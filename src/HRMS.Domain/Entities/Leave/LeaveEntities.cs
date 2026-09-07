using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Leave;

public class LeaveType : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsPaidLeave { get; set; } = true;
    public bool IsCarryForward { get; set; }
    public int MaxCarryForwardDays { get; set; }
    public bool AllowNegativeBalance { get; set; }
    public bool IsEncashable { get; set; }
    public int? MaxLeavesAllowedPerYear { get; set; }
}

/// <summary>How many days of a given LeaveType an Employee has for a given period.</summary>
public class LeaveAllocation : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public decimal AllocatedDays { get; set; }
    public decimal CarryForwardedDays { get; set; }
    public LeaveAllocationType AllocationType { get; set; } = LeaveAllocationType.AllocatedManually;

    /// <summary>Computed: AllocatedDays + CarryForwardedDays - (days used, resolved via ledger at query time).</summary>
    public decimal TotalAllocated => AllocatedDays + CarryForwardedDays;
}

public class LeaveApplication : SubmittableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid LeaveTypeId { get; set; }
    public LeaveType? LeaveType { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public bool IsHalfDay { get; set; }

    public decimal TotalLeaveDays { get; set; }
    public string? Reason { get; set; }

    public LeaveApplicationStatus Status { get; set; } = LeaveApplicationStatus.Open;

    public Guid? ApprovedByEmployeeId { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>Append-only ledger of leave debits/credits — equivalent to Frappe's Leave Ledger Entry.
/// This is the single source of truth for computing leave balance (never mutate allocations directly).</summary>
public class LeaveLedgerEntry : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public decimal Days { get; set; } // positive = credit, negative = debit
    public DateOnly TransactionDate { get; set; }
    public string? ReferenceType { get; set; } // "LeaveApplication", "LeaveAllocation", "LeaveEncashment"
    public Guid? ReferenceId { get; set; }
}
