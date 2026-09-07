using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Expense;
using HRMS.Domain.Enums;

namespace HRMS.Application.Expense;

public class EmployeeAdvanceService : IEmployeeAdvanceService
{
    private readonly IUnitOfWork _uow;

    public EmployeeAdvanceService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<EmployeeAdvanceDto>> GetAllAsync(CancellationToken ct = default)
    {
        var advances = await _uow.Repository<EmployeeAdvance>().GetAllAsync(ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);

        return advances
            .OrderByDescending(a => a.RequestDate)
            .Select(a => new EmployeeAdvanceDto(
                a.Id, a.EmployeeId, employeeNames.GetValueOrDefault(a.EmployeeId), a.PurposeDescription,
                a.AdvanceAmount, a.RequestDate, a.Status,
                a.ApprovedByEmployeeId, a.ApprovedByEmployeeId.HasValue ? employeeNames.GetValueOrDefault(a.ApprovedByEmployeeId.Value) : null))
            .ToList();
    }

    public async Task<Result<EmployeeAdvanceDto>> CreateAsync(CreateEmployeeAdvanceRequest request, CancellationToken ct = default)
    {
        if (request.AdvanceAmount <= 0)
            return Result<EmployeeAdvanceDto>.Failure("Jumlah advance harus lebih dari 0.");

        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct);
        if (employee is null)
            return Result<EmployeeAdvanceDto>.Failure("Pegawai tidak ditemukan.");

        var advance = new EmployeeAdvance
        {
            EmployeeId = request.EmployeeId,
            PurposeDescription = request.PurposeDescription,
            AdvanceAmount = request.AdvanceAmount,
            RequestDate = DateOnly.FromDateTime(DateTime.Today),
            Status = EmployeeAdvanceStatus.Pending,
            DocStatus = DocumentStatus.Submitted
        };

        await _uow.Repository<EmployeeAdvance>().AddAsync(advance, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<EmployeeAdvanceDto>.Success(new EmployeeAdvanceDto(
            advance.Id, advance.EmployeeId, employee.FullName, advance.PurposeDescription,
            advance.AdvanceAmount, advance.RequestDate, advance.Status, null, null));
    }

    public async Task<Result> ApproveAsync(Guid advanceId, Guid approverEmployeeId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<EmployeeAdvance>();
        var advance = await repo.GetByIdAsync(advanceId, ct);
        if (advance is null)
            return Result.Failure("Pengajuan advance tidak ditemukan.");
        if (advance.Status != EmployeeAdvanceStatus.Pending)
            return Result.Failure("Hanya advance berstatus Pending yang bisa disetujui.");

        advance.Status = EmployeeAdvanceStatus.Approved;
        advance.ApprovedByEmployeeId = approverEmployeeId;
        repo.Update(advance);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid advanceId, Guid approverEmployeeId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<EmployeeAdvance>();
        var advance = await repo.GetByIdAsync(advanceId, ct);
        if (advance is null)
            return Result.Failure("Pengajuan advance tidak ditemukan.");
        if (advance.Status != EmployeeAdvanceStatus.Pending)
            return Result.Failure("Hanya advance berstatus Pending yang bisa ditolak.");

        advance.Status = EmployeeAdvanceStatus.Rejected;
        advance.ApprovedByEmployeeId = approverEmployeeId;
        repo.Update(advance);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> MarkPaidAsync(Guid advanceId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<EmployeeAdvance>();
        var advance = await repo.GetByIdAsync(advanceId, ct);
        if (advance is null)
            return Result.Failure("Pengajuan advance tidak ditemukan.");
        if (advance.Status != EmployeeAdvanceStatus.Approved)
            return Result.Failure("Hanya advance yang sudah disetujui yang bisa ditandai lunas dibayar.");

        advance.Status = EmployeeAdvanceStatus.Paid;
        repo.Update(advance);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
