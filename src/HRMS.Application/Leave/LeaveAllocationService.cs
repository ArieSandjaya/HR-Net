using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Leave;

namespace HRMS.Application.Leave;

public class LeaveAllocationService : ILeaveAllocationService
{
    private readonly IUnitOfWork _uow;

    public LeaveAllocationService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<LeaveAllocationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var allocations = await _uow.Repository<LeaveAllocation>().GetAllAsync(ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var leaveTypes = await _uow.Repository<LeaveType>().GetAllAsync(ct);

        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);
        var leaveTypeNames = leaveTypes.ToDictionary(lt => lt.Id, lt => lt.Name);

        return allocations
            .OrderByDescending(a => a.FromDate)
            .Select(a => new LeaveAllocationDto(
                a.Id, a.EmployeeId, employeeNames.GetValueOrDefault(a.EmployeeId),
                a.LeaveTypeId, leaveTypeNames.GetValueOrDefault(a.LeaveTypeId),
                a.FromDate, a.ToDate, a.AllocatedDays, a.CarryForwardedDays))
            .ToList();
    }

    public async Task<Result<LeaveAllocationDto>> AllocateAsync(AllocateLeaveRequest request, CancellationToken ct = default)
    {
        if (request.AllocatedDays <= 0)
            return Result<LeaveAllocationDto>.Failure("Jumlah hari yang dialokasikan harus lebih dari 0.");
        if (request.ToDate < request.FromDate)
            return Result<LeaveAllocationDto>.Failure("Tanggal selesai periode tidak boleh sebelum tanggal mulai.");

        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct);
        if (employee is null)
            return Result<LeaveAllocationDto>.Failure("Pegawai tidak ditemukan.");

        var leaveType = await _uow.Repository<LeaveType>().GetByIdAsync(request.LeaveTypeId, ct);
        if (leaveType is null)
            return Result<LeaveAllocationDto>.Failure("Jenis cuti tidak ditemukan.");

        // Avoid double-allocating the same employee+leave type for an overlapping period.
        var overlapping = await _uow.Repository<LeaveAllocation>().FindAsync(
            a => a.EmployeeId == request.EmployeeId && a.LeaveTypeId == request.LeaveTypeId
                 && a.FromDate <= request.ToDate && a.ToDate >= request.FromDate, ct);
        if (overlapping.Count > 0)
            return Result<LeaveAllocationDto>.Failure("Pegawai ini sudah punya alokasi jenis cuti yang sama pada periode yang tumpang tindih.");

        var allocation = new LeaveAllocation
        {
            EmployeeId = request.EmployeeId,
            LeaveTypeId = request.LeaveTypeId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            AllocatedDays = request.AllocatedDays,
            AllocationType = Domain.Enums.LeaveAllocationType.AllocatedManually
        };
        await _uow.Repository<LeaveAllocation>().AddAsync(allocation, ct);

        // This is the credit entry GetLeaveBalanceAsync sums up. Without it, the ledger would
        // only ever accumulate debits (from approved LeaveApplications) and balance would
        // always be zero or negative — this was the actual bug.
        await _uow.Repository<LeaveLedgerEntry>().AddAsync(new LeaveLedgerEntry
        {
            EmployeeId = request.EmployeeId,
            LeaveTypeId = request.LeaveTypeId,
            Days = request.AllocatedDays,
            TransactionDate = request.FromDate,
            ReferenceType = nameof(LeaveAllocation),
            ReferenceId = allocation.Id
        }, ct);

        await _uow.SaveChangesAsync(ct);

        return Result<LeaveAllocationDto>.Success(new LeaveAllocationDto(
            allocation.Id, allocation.EmployeeId, employee.FullName,
            allocation.LeaveTypeId, leaveType.Name,
            allocation.FromDate, allocation.ToDate, allocation.AllocatedDays, allocation.CarryForwardedDays));
    }
}
