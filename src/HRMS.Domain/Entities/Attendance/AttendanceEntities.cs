using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Attendance;

public class ShiftType : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int LateEntryGraceMinutes { get; set; } = 10;
    public int EarlyExitGraceMinutes { get; set; } = 10;
}

public class ShiftAssignment : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid ShiftTypeId { get; set; }
    public ShiftType? ShiftType { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public ShiftAssignmentStatus Status { get; set; } = ShiftAssignmentStatus.Active;
}

/// <summary>Raw in/out punch, e.g. from biometric device, mobile geolocation, or web check-in.</summary>
public class EmployeeCheckin : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateTime Timestamp { get; set; }
    public bool IsCheckIn { get; set; } // true = IN, false = OUT

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? DeviceId { get; set; }
}

/// <summary>Daily aggregate attendance record, derived from checkins or marked manually.</summary>
public class AttendanceRecord : SubmittableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateOnly AttendanceDate { get; set; }
    public AttendanceStatus Status { get; set; }

    public TimeSpan? WorkingHours { get; set; }
    public Guid? ShiftTypeId { get; set; }
    public string? Remarks { get; set; }
}
