using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Application.Leave;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Expense;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Entities.Separation;
using HRMS.Domain.Enums;

namespace HRMS.Application.Separation;

public class FnFSettlementService : IFnFSettlementService
{
    private readonly IUnitOfWork _uow;
    private readonly ILeaveApplicationService _leaveApplicationService;

    public FnFSettlementService(IUnitOfWork uow, ILeaveApplicationService leaveApplicationService)
    {
        _uow = uow;
        _leaveApplicationService = leaveApplicationService;
    }

    public async Task<FullAndFinalSettlementDto?> GetBySeparationAsync(Guid separationId, CancellationToken ct = default)
    {
        var settlement = (await _uow.Repository<FullAndFinalSettlement>().FindAsync(f => f.EmployeeSeparationId == separationId, ct)).FirstOrDefault();
        if (settlement is null) return null;

        var employee = await _uow.Repository<Employee>().GetByIdAsync(settlement.EmployeeId, ct);
        var components = await _uow.Repository<FnFComponent>().FindAsync(c => c.FullAndFinalSettlementId == settlement.Id, ct);

        return MapToDto(settlement, employee?.FullName, components.ToList());
    }

    public async Task<Result<FullAndFinalSettlementDto>> GenerateAsync(Guid separationId, CancellationToken ct = default)
    {
        var separation = await _uow.Repository<EmployeeSeparation>().GetByIdAsync(separationId, ct);
        if (separation is null)
            return Result<FullAndFinalSettlementDto>.Failure("Data separation tidak ditemukan.");

        var existing = await _uow.Repository<FullAndFinalSettlement>().FindAsync(f => f.EmployeeSeparationId == separationId, ct);
        if (existing.Count > 0)
            return Result<FullAndFinalSettlementDto>.Failure("Settlement untuk separation ini sudah pernah dibuat.");

        var employee = await _uow.Repository<Employee>().GetByIdAsync(separation.EmployeeId, ct);
        if (employee is null)
            return Result<FullAndFinalSettlementDto>.Failure("Pegawai tidak ditemukan.");

        var asOfDate = separation.RelievingDate ?? DateOnly.FromDateTime(DateTime.Today);

        var settlement = new FullAndFinalSettlement
        {
            EmployeeSeparationId = separationId,
            EmployeeId = separation.EmployeeId,
            Status = FnFStatus.Draft
        };
        await _uow.Repository<FullAndFinalSettlement>().AddAsync(settlement, ct);

        var components = new List<FnFComponent>();

        // Leave encashment: sum current balance across every LeaveType marked IsEncashable,
        // priced at the employee's latest per-day base salary rate. IsEncashable has existed
        // on LeaveType since Phase 1 but nothing ever read it until this integration.
        var assignments = await _uow.Repository<SalaryStructureAssignment>().FindAsync(
            a => a.EmployeeId == separation.EmployeeId && a.FromDate <= asOfDate, ct);
        var latestAssignment = assignments.OrderByDescending(a => a.FromDate).FirstOrDefault();

        if (latestAssignment is not null)
        {
            var perDayRate = latestAssignment.BaseSalary / 30m;
            var encashableLeaveTypes = await _uow.Repository<LeaveType>().FindAsync(lt => lt.IsEncashable, ct);

            decimal totalEncashableDays = 0;
            foreach (var leaveType in encashableLeaveTypes)
            {
                var balance = await _leaveApplicationService.GetLeaveBalanceAsync(separation.EmployeeId, leaveType.Id, asOfDate, ct);
                if (balance > 0)
                    totalEncashableDays += balance;
            }

            if (totalEncashableDays > 0)
            {
                components.Add(new FnFComponent
                {
                    FullAndFinalSettlementId = settlement.Id,
                    Description = $"Uang pengganti cuti ({totalEncashableDays} hari)",
                    ComponentType = SalaryComponentType.Earning,
                    Amount = Math.Round(perDayRate * totalEncashableDays, 0)
                });
            }
        }

        // Deduction: any cash advance already paid out but never settled against an expense claim.
        var unsettledAdvances = await _uow.Repository<EmployeeAdvance>().FindAsync(
            a => a.EmployeeId == separation.EmployeeId && a.Status == EmployeeAdvanceStatus.Paid, ct);
        foreach (var advance in unsettledAdvances)
        {
            components.Add(new FnFComponent
            {
                FullAndFinalSettlementId = settlement.Id,
                Description = $"Advance belum diselesaikan: {advance.PurposeDescription}",
                ComponentType = SalaryComponentType.Deduction,
                Amount = advance.AdvanceAmount
            });
        }

        foreach (var component in components)
            await _uow.Repository<FnFComponent>().AddAsync(component, ct);

        RecomputeTotals(settlement, components);
        _uow.Repository<FullAndFinalSettlement>().Update(settlement);

        await _uow.SaveChangesAsync(ct);

        return Result<FullAndFinalSettlementDto>.Success(MapToDto(settlement, employee.FullName, components));
    }

    public async Task<Result> AddComponentAsync(AddFnFComponentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            return Result.Failure("Nominal harus lebih dari 0.");

        var settlement = await _uow.Repository<FullAndFinalSettlement>().GetByIdAsync(request.FullAndFinalSettlementId, ct);
        if (settlement is null)
            return Result.Failure("Settlement tidak ditemukan.");
        if (settlement.Status != FnFStatus.Draft)
            return Result.Failure("Settlement yang sudah difinalisasi tidak bisa diubah lagi.");

        await _uow.Repository<FnFComponent>().AddAsync(new FnFComponent
        {
            FullAndFinalSettlementId = request.FullAndFinalSettlementId,
            Description = request.Description,
            ComponentType = request.ComponentType,
            Amount = request.Amount
        }, ct);

        // Update totals incrementally rather than re-querying FnFComponent — a fresh query
        // wouldn't see the component just added above since SaveChangesAsync hasn't run yet.
        if (request.ComponentType == SalaryComponentType.Earning)
            settlement.TotalEarnings += request.Amount;
        else
            settlement.TotalDeductions += request.Amount;
        settlement.NetPayable = settlement.TotalEarnings - settlement.TotalDeductions;

        _uow.Repository<FullAndFinalSettlement>().Update(settlement);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> FinalizeAsync(Guid settlementId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<FullAndFinalSettlement>();
        var settlement = await repo.GetByIdAsync(settlementId, ct);
        if (settlement is null)
            return Result.Failure("Settlement tidak ditemukan.");
        if (settlement.Status != FnFStatus.Draft)
            return Result.Failure("Settlement ini sudah difinalisasi.");

        settlement.Status = FnFStatus.Finalized;
        settlement.DocStatus = DocumentStatus.Submitted;
        repo.Update(settlement);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void RecomputeTotals(FullAndFinalSettlement settlement, List<FnFComponent> components)
    {
        settlement.TotalEarnings = components.Where(c => c.ComponentType == SalaryComponentType.Earning).Sum(c => c.Amount);
        settlement.TotalDeductions = components.Where(c => c.ComponentType == SalaryComponentType.Deduction).Sum(c => c.Amount);
        settlement.NetPayable = settlement.TotalEarnings - settlement.TotalDeductions;
    }

    private static FullAndFinalSettlementDto MapToDto(FullAndFinalSettlement s, string? employeeName, List<FnFComponent> components) => new(
        s.Id, s.EmployeeSeparationId, s.EmployeeId, employeeName,
        s.TotalEarnings, s.TotalDeductions, s.NetPayable, s.Status,
        components.Select(c => new FnFComponentDto(c.Id, c.Description, c.ComponentType, c.Amount)).ToList());
}
