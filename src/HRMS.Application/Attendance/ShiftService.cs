using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Employees;

namespace HRMS.Application.Attendance;

public class ShiftService : IShiftService
{
    private readonly IUnitOfWork _uow;

    public ShiftService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<ShiftTypeDto>> GetShiftTypesAsync(CancellationToken ct = default)
    {
        var shiftTypes = await _uow.Repository<ShiftType>().GetAllAsync(ct);
        return shiftTypes.Select(s => new ShiftTypeDto(s.Id, s.Name, s.StartTime, s.EndTime, s.LateEntryGraceMinutes, s.EarlyExitGraceMinutes)).ToList();
    }

    public async Task<Result<ShiftTypeDto>> CreateShiftTypeAsync(CreateShiftTypeRequest request, CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
            return Result<ShiftTypeDto>.Failure("Jam selesai shift harus setelah jam mulai.");

        var shiftType = new ShiftType
        {
            Name = request.Name,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            LateEntryGraceMinutes = request.LateEntryGraceMinutes,
            EarlyExitGraceMinutes = request.EarlyExitGraceMinutes
        };

        await _uow.Repository<ShiftType>().AddAsync(shiftType, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ShiftTypeDto>.Success(new ShiftTypeDto(
            shiftType.Id, shiftType.Name, shiftType.StartTime, shiftType.EndTime,
            shiftType.LateEntryGraceMinutes, shiftType.EarlyExitGraceMinutes));
    }

    public async Task<List<ShiftAssignmentDto>> GetShiftAssignmentsAsync(CancellationToken ct = default)
    {
        var assignments = await _uow.Repository<ShiftAssignment>().GetAllAsync(ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var shiftTypes = await _uow.Repository<ShiftType>().GetAllAsync(ct);

        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);
        var shiftTypeNames = shiftTypes.ToDictionary(s => s.Id, s => s.Name);

        return assignments
            .OrderByDescending(a => a.StartDate)
            .Select(a => new ShiftAssignmentDto(
                a.Id, a.EmployeeId, employeeNames.GetValueOrDefault(a.EmployeeId),
                a.ShiftTypeId, shiftTypeNames.GetValueOrDefault(a.ShiftTypeId),
                a.StartDate, a.EndDate, a.Status))
            .ToList();
    }

    public async Task<Result<ShiftAssignmentDto>> AssignShiftAsync(AssignShiftRequest request, CancellationToken ct = default)
    {
        if (request.EndDate.HasValue && request.EndDate < request.StartDate)
            return Result<ShiftAssignmentDto>.Failure("Tanggal selesai tidak boleh sebelum tanggal mulai.");

        // Prevent overlapping active shift assignments for the same employee.
        var overlapping = await _uow.Repository<ShiftAssignment>().FindAsync(
            sa => sa.EmployeeId == request.EmployeeId
                  && sa.Status == HRMS.Domain.Enums.ShiftAssignmentStatus.Active
                  && (sa.EndDate == null || sa.EndDate >= request.StartDate)
                  && (request.EndDate == null || sa.StartDate <= request.EndDate), ct);
        if (overlapping.Count > 0)
            return Result<ShiftAssignmentDto>.Failure("Pegawai sudah punya penugasan shift aktif yang tumpang tindih pada rentang tanggal ini.");

        var assignment = new ShiftAssignment
        {
            EmployeeId = request.EmployeeId,
            ShiftTypeId = request.ShiftTypeId,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        await _uow.Repository<ShiftAssignment>().AddAsync(assignment, ct);
        await _uow.SaveChangesAsync(ct);

        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct);
        var shiftType = await _uow.Repository<ShiftType>().GetByIdAsync(request.ShiftTypeId, ct);

        return Result<ShiftAssignmentDto>.Success(new ShiftAssignmentDto(
            assignment.Id, assignment.EmployeeId, employee?.FullName,
            assignment.ShiftTypeId, shiftType?.Name, assignment.StartDate, assignment.EndDate, assignment.Status));
    }
}
