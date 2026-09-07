using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Expense;

/// <summary>Reference list of expense categories (e.g. "Transportasi", "Akomodasi", "Makan").</summary>
public class ExpenseCategory : TenantEntity
{
    public string Name { get; set; } = string.Empty;
}

/// <summary>An employee's claim for reimbursement, made up of one or more line items.
/// Goes through a single-approver workflow (see README for why this isn't a true
/// multi-level/threshold-based approval chain).</summary>
public class ExpenseClaim : SubmittableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateOnly ClaimDate { get; set; }
    public decimal TotalAmount { get; set; }
    public ExpenseClaimStatus Status { get; set; } = ExpenseClaimStatus.Pending;

    public Guid? ApprovedByEmployeeId { get; set; }
    public Employee? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>If this claim settles a prior cash advance, links back to it — the advance's
    /// remaining balance (AdvanceAmount - TotalAmount here) is the employee's responsibility
    /// to return, though actual cash reconciliation is left to Finance outside this system.</summary>
    public Guid? SettlesAdvanceId { get; set; }
    public EmployeeAdvance? SettlesAdvance { get; set; }

    public ICollection<ExpenseClaimDetail> Details { get; set; } = new List<ExpenseClaimDetail>();
}

public class ExpenseClaimDetail : TenantEntity
{
    public Guid ExpenseClaimId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public Guid ExpenseCategoryId { get; set; }
    public ExpenseCategory? ExpenseCategory { get; set; }

    public DateOnly ExpenseDate { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>A cash advance given to an employee ahead of travel/expenses, later expected to
/// be settled against an ExpenseClaim.</summary>
public class EmployeeAdvance : SubmittableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string PurposeDescription { get; set; } = string.Empty;
    public decimal AdvanceAmount { get; set; }
    public DateOnly RequestDate { get; set; }
    public EmployeeAdvanceStatus Status { get; set; } = EmployeeAdvanceStatus.Pending;

    public Guid? ApprovedByEmployeeId { get; set; }
    public Employee? ApprovedBy { get; set; }
}

public class TravelRequest : SubmittableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public string Destination { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public TravelRequestStatus Status { get; set; } = TravelRequestStatus.Pending;

    public Guid? ApprovedByEmployeeId { get; set; }
    public Employee? ApprovedBy { get; set; }
}
