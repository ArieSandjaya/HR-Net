using HRMS.Application.Common.Models;

namespace HRMS.Application.Attendance;

public interface IAttendanceService
{
    Task<Result> CheckInAsync(Guid employeeId, double? latitude, double? longitude, string? deviceId, CancellationToken ct = default);
    Task<Result> CheckOutAsync(Guid employeeId, double? latitude, double? longitude, string? deviceId, CancellationToken ct = default);
    Task<List<CheckinDto>> GetTodayCheckinsAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>Aggregates a single employee's checkins (and any approved leave) for one date
    /// into an AttendanceRecord. Idempotent — re-running for the same date updates the existing record.</summary>
    Task<AttendanceRecordDto> GenerateAttendanceForDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default);

    /// <summary>Runs GenerateAttendanceForDateAsync for every active employee — intended to be
    /// invoked by a daily Hangfire recurring job (see Program.cs).</summary>
    Task<int> GenerateAttendanceForAllEmployeesAsync(DateOnly date, CancellationToken ct = default);

    Task<List<AttendanceRecordDto>> GetAttendanceRecordsAsync(Guid? employeeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default);
}

public interface IShiftService
{
    Task<List<ShiftTypeDto>> GetShiftTypesAsync(CancellationToken ct = default);
    Task<Result<ShiftTypeDto>> CreateShiftTypeAsync(CreateShiftTypeRequest request, CancellationToken ct = default);

    Task<List<ShiftAssignmentDto>> GetShiftAssignmentsAsync(CancellationToken ct = default);
    Task<Result<ShiftAssignmentDto>> AssignShiftAsync(AssignShiftRequest request, CancellationToken ct = default);
}
