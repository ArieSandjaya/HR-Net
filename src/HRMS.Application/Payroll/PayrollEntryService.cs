using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Enums;

namespace HRMS.Application.Payroll;

public class PayrollEntryService : IPayrollEntryService
{
    private readonly IUnitOfWork _uow;

    public PayrollEntryService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<PayrollEntryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var entries = await _uow.Repository<PayrollEntry>().GetAllAsync(ct);
        var companies = await _uow.Repository<Company>().GetAllAsync(ct);
        var companyNames = companies.ToDictionary(c => c.Id, c => c.Name);
        var allSlips = await _uow.Repository<SalarySlip>().GetAllAsync(ct);

        return entries
            .OrderByDescending(e => e.PeriodStart)
            .Select(e =>
            {
                var slips = allSlips.Where(s => s.PayrollEntryId == e.Id).ToList();
                return new PayrollEntryDto(
                    e.Id, e.CompanyId, companyNames.GetValueOrDefault(e.CompanyId),
                    e.PeriodStart, e.PeriodEnd, e.DocStatus, slips.Count, slips.Sum(s => s.NetPay));
            })
            .ToList();
    }

    public async Task<List<SalarySlipDto>> GetSalarySlipsAsync(Guid payrollEntryId, CancellationToken ct = default)
    {
        var slips = await _uow.Repository<SalarySlip>().FindAsync(s => s.PayrollEntryId == payrollEntryId, ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeLookup = employees.ToDictionary(e => e.Id);
        var allComponents = await _uow.Repository<SalarySlipComponent>().FindAsync(
            c => slips.Select(s => s.Id).Contains(c.SalarySlipId), ct);

        return slips
            .OrderBy(s => employeeLookup.GetValueOrDefault(s.EmployeeId)?.FullName)
            .Select(s =>
            {
                employeeLookup.TryGetValue(s.EmployeeId, out var emp);
                var components = allComponents
                    .Where(c => c.SalarySlipId == s.Id)
                    .Select(c => new SalarySlipComponentDto(c.ComponentName, c.ComponentType, c.Amount))
                    .ToList();

                return new SalarySlipDto(
                    s.Id, s.EmployeeId, emp?.FullName, emp?.EmployeeNumber,
                    s.PeriodStart, s.PeriodEnd, s.BaseSalary, s.UnpaidAbsentDays, s.WorkingDaysInPeriod,
                    s.GrossPay, s.TotalDeductions, s.NetPay, components);
            })
            .ToList();
    }

    public async Task<Result<PayrollEntryDto>> RunPayrollAsync(CreatePayrollEntryRequest request, CancellationToken ct = default)
    {
        if (request.PeriodEnd < request.PeriodStart)
            return Result<PayrollEntryDto>.Failure("Tanggal akhir periode tidak boleh sebelum tanggal mulai.");

        var company = await _uow.Repository<Company>().GetByIdAsync(request.CompanyId, ct);
        if (company is null)
            return Result<PayrollEntryDto>.Failure("Perusahaan tidak ditemukan.");

        var duplicate = await _uow.Repository<PayrollEntry>().FindAsync(
            e => e.CompanyId == request.CompanyId && e.PeriodStart == request.PeriodStart && e.PeriodEnd == request.PeriodEnd, ct);
        if (duplicate.Count > 0)
            return Result<PayrollEntryDto>.Failure("Payroll untuk perusahaan dan periode ini sudah pernah dijalankan.");

        // Pick, per employee, the most recent SalaryStructureAssignment on or before PeriodStart.
        var allAssignments = await _uow.Repository<SalaryStructureAssignment>().GetAllAsync(ct);
        var activeAssignments = allAssignments
            .Where(a => a.FromDate <= request.PeriodStart)
            .GroupBy(a => a.EmployeeId)
            .Select(g => g.OrderByDescending(a => a.FromDate).First())
            .ToList();

        var employees = await _uow.Repository<Employee>().FindAsync(
            e => e.CompanyId == request.CompanyId && e.Status == EmployeeStatus.Active, ct);
        var employeeIds = employees.Select(e => e.Id).ToHashSet();

        var payableAssignments = activeAssignments.Where(a => employeeIds.Contains(a.EmployeeId)).ToList();
        if (payableAssignments.Count == 0)
            return Result<PayrollEntryDto>.Failure("Tidak ada pegawai aktif dengan penugasan struktur gaji untuk perusahaan ini.");

        var structures = await _uow.Repository<SalaryStructure>().GetAllAsync(ct);
        var structureComponents = await _uow.Repository<SalaryStructureComponent>().GetAllAsync(ct);
        var salaryComponents = await _uow.Repository<SalaryComponent>().GetAllAsync(ct);
        var salaryComponentLookup = salaryComponents.ToDictionary(c => c.Id);

        var attendanceRecords = await _uow.Repository<AttendanceRecord>().FindAsync(
            a => a.AttendanceDate >= request.PeriodStart && a.AttendanceDate <= request.PeriodEnd, ct);

        var workingDays = CountWorkingDays(request.PeriodStart, request.PeriodEnd);

        var payrollEntry = new PayrollEntry
        {
            CompanyId = request.CompanyId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            DocStatus = DocumentStatus.Submitted
        };
        await _uow.Repository<PayrollEntry>().AddAsync(payrollEntry, ct);

        foreach (var assignment in payableAssignments)
        {
            var absentDays = attendanceRecords.Count(a => a.EmployeeId == assignment.EmployeeId && a.Status == AttendanceStatus.Absent);

            var perDayRate = workingDays > 0 ? assignment.BaseSalary / workingDays : 0;
            var proratedBase = Math.Max(0, assignment.BaseSalary - perDayRate * absentDays);

            var slip = new SalarySlip
            {
                PayrollEntryId = payrollEntry.Id,
                EmployeeId = assignment.EmployeeId,
                PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd,
                BaseSalary = proratedBase,
                UnpaidAbsentDays = absentDays,
                WorkingDaysInPeriod = workingDays,
                DocStatus = DocumentStatus.Submitted
            };

            decimal totalEarnings = proratedBase;
            decimal totalDeductions = 0;
            var slipComponents = new List<SalarySlipComponent>();

            var thisStructureComponents = structureComponents.Where(sc => sc.SalaryStructureId == assignment.SalaryStructureId);
            foreach (var sc in thisStructureComponents)
            {
                if (!salaryComponentLookup.TryGetValue(sc.SalaryComponentId, out var component))
                    continue;

                var amount = component.CalculationType == SalaryCalculationType.FixedAmount
                    ? sc.AmountOverride ?? component.DefaultAmount ?? 0
                    : proratedBase * (sc.PercentageOverride ?? component.DefaultPercentage ?? 0) / 100m;

                if (component.ComponentType == SalaryComponentType.Earning)
                    totalEarnings += amount;
                else
                    totalDeductions += amount;

                slipComponents.Add(new SalarySlipComponent
                {
                    SalarySlipId = slip.Id,
                    SalaryComponentId = component.Id,
                    ComponentName = component.Name,
                    ComponentType = component.ComponentType,
                    Amount = amount
                });
            }

            slip.GrossPay = totalEarnings;
            slip.TotalDeductions = totalDeductions;
            slip.NetPay = totalEarnings - totalDeductions;

            await _uow.Repository<SalarySlip>().AddAsync(slip, ct);
            foreach (var sc in slipComponents)
                await _uow.Repository<SalarySlipComponent>().AddAsync(sc, ct);
        }

        await _uow.SaveChangesAsync(ct);

        var totalNetPay = payableAssignments.Count == 0 ? 0 : (await GetSalarySlipsAsync(payrollEntry.Id, ct)).Sum(s => s.NetPay);

        return Result<PayrollEntryDto>.Success(new PayrollEntryDto(
            payrollEntry.Id, payrollEntry.CompanyId, company.Name,
            payrollEntry.PeriodStart, payrollEntry.PeriodEnd, payrollEntry.DocStatus,
            payableAssignments.Count, totalNetPay));
    }

    private static int CountWorkingDays(DateOnly start, DateOnly end)
    {
        var count = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                count++;
        }
        return count;
    }
}
