using HRMS.Domain.Enums;

namespace HRMS.Application.Attendance;

public record CheckinDto(Guid Id, DateTime Timestamp, bool IsCheckIn, double? Latitude, double? Longitude);

public record AttendanceRecordDto(
    Guid Id,
    Guid EmployeeId,
    string? EmployeeName,
    DateOnly AttendanceDate,
    AttendanceStatus Status,
    TimeSpan? WorkingHours,
    string? Remarks
);

public record ShiftTypeDto(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime, int LateEntryGraceMinutes, int EarlyExitGraceMinutes);

public record CreateShiftTypeRequest(string Name, TimeOnly StartTime, TimeOnly EndTime, int LateEntryGraceMinutes, int EarlyExitGraceMinutes);

public record ShiftAssignmentDto(
    Guid Id,
    Guid EmployeeId,
    string? EmployeeName,
    Guid ShiftTypeId,
    string? ShiftTypeName,
    DateOnly StartDate,
    DateOnly? EndDate,
    ShiftAssignmentStatus Status
);

public record AssignShiftRequest(Guid EmployeeId, Guid ShiftTypeId, DateOnly StartDate, DateOnly? EndDate);
