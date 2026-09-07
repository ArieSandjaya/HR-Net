using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Enums;

namespace HRMS.Application.Leave;

public record ApplyLeaveRequest(
    Guid EmployeeId,
    Guid LeaveTypeId,
    DateOnly FromDate,
    DateOnly ToDate,
    bool IsHalfDay,
    string? Reason
);

public record LeaveApplicationDto(
    Guid Id,
    Guid EmployeeId,
    string? EmployeeName,
    Guid LeaveTypeId,
    string? LeaveTypeName,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal TotalLeaveDays,
    LeaveApplicationStatus Status
);

public interface ILeaveApplicationService
{
    Task<decimal> GetLeaveBalanceAsync(Guid employeeId, Guid leaveTypeId, DateOnly asOfDate, CancellationToken ct = default);
    Task<Result<LeaveApplicationDto>> ApplyAsync(ApplyLeaveRequest request, CancellationToken ct = default);
    Task<Result> ApproveAsync(Guid leaveApplicationId, Guid approverEmployeeId, CancellationToken ct = default);
    Task<Result> RejectAsync(Guid leaveApplicationId, Guid approverEmployeeId, string reason, CancellationToken ct = default);
}

// ---- Leave Allocation ----
// BUG FIX: this was entirely missing. The LeaveAllocation entity existed in the Domain layer
// and GetLeaveBalanceAsync above was already summing the ledger correctly, but nothing ever
// WROTE a credit entry to the ledger — meaning every employee's leave balance was always
// zero (or negative), and applying for any leave type with AllowNegativeBalance=false
// (e.g. the default "Cuti Tahunan") would always fail with "insufficient balance".

public record LeaveAllocationDto(
    Guid Id, Guid EmployeeId, string? EmployeeName, Guid LeaveTypeId, string? LeaveTypeName,
    DateOnly FromDate, DateOnly ToDate, decimal AllocatedDays, decimal CarryForwardedDays
);

public record AllocateLeaveRequest(Guid EmployeeId, Guid LeaveTypeId, DateOnly FromDate, DateOnly ToDate, decimal AllocatedDays);

public interface ILeaveAllocationService
{
    Task<List<LeaveAllocationDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<LeaveAllocationDto>> AllocateAsync(AllocateLeaveRequest request, CancellationToken ct = default);
}

public class LeaveApplicationService : ILeaveApplicationService
{
    private readonly IUnitOfWork _uow;

    public LeaveApplicationService(IUnitOfWork uow) => _uow = uow;

    /// <summary>
    /// Balance = SUM(LeaveLedgerEntry.Days) for the employee+leave type up to the given date.
    /// The ledger is append-only, so this is always an accurate point-in-time balance
    /// (mirrors Frappe's "Leave Ledger Entry" pattern instead of mutating a running total).
    /// </summary>
    public async Task<decimal> GetLeaveBalanceAsync(Guid employeeId, Guid leaveTypeId, DateOnly asOfDate, CancellationToken ct = default)
    {
        var entries = await _uow.Repository<LeaveLedgerEntry>().FindAsync(
            e => e.EmployeeId == employeeId && e.LeaveTypeId == leaveTypeId && e.TransactionDate <= asOfDate, ct);

        return entries.Sum(e => e.Days);
    }

    public async Task<Result<LeaveApplicationDto>> ApplyAsync(ApplyLeaveRequest request, CancellationToken ct = default)
    {
        if (request.ToDate < request.FromDate)
            return Result<LeaveApplicationDto>.Failure("Tanggal selesai tidak boleh sebelum tanggal mulai.");

        var totalDays = request.IsHalfDay
            ? 0.5m
            : (request.ToDate.DayNumber - request.FromDate.DayNumber + 1);

        var leaveTypeRepo = _uow.Repository<LeaveType>();
        var leaveType = await leaveTypeRepo.GetByIdAsync(request.LeaveTypeId, ct);
        if (leaveType is null)
            return Result<LeaveApplicationDto>.Failure("Jenis cuti tidak ditemukan.");

        // Overlap check: reject if employee already has an approved/open application in this range.
        var existing = await _uow.Repository<LeaveApplication>().FindAsync(
            la => la.EmployeeId == request.EmployeeId
                  && la.Status != LeaveApplicationStatus.Rejected
                  && la.Status != LeaveApplicationStatus.Cancelled
                  && la.FromDate <= request.ToDate
                  && la.ToDate >= request.FromDate, ct);
        if (existing.Count > 0)
            return Result<LeaveApplicationDto>.Failure("Sudah ada pengajuan cuti yang tumpang tindih pada rentang tanggal ini.");

        if (!leaveType.AllowNegativeBalance)
        {
            var balance = await GetLeaveBalanceAsync(request.EmployeeId, request.LeaveTypeId, request.FromDate, ct);
            if (balance < totalDays)
                return Result<LeaveApplicationDto>.Failure(
                    $"Saldo cuti tidak mencukupi. Saldo tersedia: {balance} hari, diajukan: {totalDays} hari.");
        }

        var application = new LeaveApplication
        {
            EmployeeId = request.EmployeeId,
            LeaveTypeId = request.LeaveTypeId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            IsHalfDay = request.IsHalfDay,
            TotalLeaveDays = totalDays,
            Reason = request.Reason,
            Status = LeaveApplicationStatus.Open
        };

        await _uow.Repository<LeaveApplication>().AddAsync(application, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<LeaveApplicationDto>.Success(new LeaveApplicationDto(
            application.Id, application.EmployeeId, null, application.LeaveTypeId, leaveType.Name,
            application.FromDate, application.ToDate, application.TotalLeaveDays, application.Status));
    }

    public async Task<Result> ApproveAsync(Guid leaveApplicationId, Guid approverEmployeeId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<LeaveApplication>();
        var application = await repo.GetByIdAsync(leaveApplicationId, ct);
        if (application is null)
            return Result.Failure("Pengajuan cuti tidak ditemukan.");
        if (application.Status != LeaveApplicationStatus.Open)
            return Result.Failure("Hanya pengajuan berstatus Open yang bisa disetujui.");

        application.Status = LeaveApplicationStatus.Approved;
        application.ApprovedByEmployeeId = approverEmployeeId;
        application.ApprovedAt = DateTime.UtcNow;
        application.DocStatus = Domain.Common.DocumentStatus.Submitted;
        repo.Update(application);

        // Post debit entry to the leave ledger — this is what actually reduces the balance.
        await _uow.Repository<LeaveLedgerEntry>().AddAsync(new LeaveLedgerEntry
        {
            EmployeeId = application.EmployeeId,
            LeaveTypeId = application.LeaveTypeId,
            Days = -application.TotalLeaveDays,
            TransactionDate = application.FromDate,
            ReferenceType = nameof(LeaveApplication),
            ReferenceId = application.Id
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid leaveApplicationId, Guid approverEmployeeId, string reason, CancellationToken ct = default)
    {
        var repo = _uow.Repository<LeaveApplication>();
        var application = await repo.GetByIdAsync(leaveApplicationId, ct);
        if (application is null)
            return Result.Failure("Pengajuan cuti tidak ditemukan.");
        if (application.Status != LeaveApplicationStatus.Open)
            return Result.Failure("Hanya pengajuan berstatus Open yang bisa ditolak.");

        application.Status = LeaveApplicationStatus.Rejected;
        application.ApprovedByEmployeeId = approverEmployeeId;
        application.RejectionReason = reason;
        repo.Update(application);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }
}
