using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Separation;

/// <summary>The formal record of an employee leaving the organization — resignation,
/// termination, retirement, or end of contract. Approving it fixes the RelievingDate;
/// completing it is what actually flips the Employee record to LeftOrganization
/// (see EmployeeSeparationService, which reuses EmployeeService.MarkAsRelievedAsync —
/// present since Phase 1 but never wired into a workflow until now).</summary>
public class EmployeeSeparation : SubmittableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    /// <summary>Date the separation was requested/initiated.</summary>
    public DateOnly ResignationDate { get; set; }

    /// <summary>The employee's confirmed last working day — set when the separation is approved.</summary>
    public DateOnly? RelievingDate { get; set; }

    public string? Reason { get; set; }
    public SeparationType SeparationType { get; set; } = SeparationType.Resignation;
    public SeparationStatus Status { get; set; } = SeparationStatus.Pending;

    public Guid? ApprovedByEmployeeId { get; set; }
    public Employee? ApprovedBy { get; set; }

    public ICollection<ExitInterview> ExitInterviews { get; set; } = new List<ExitInterview>();
}

public class ExitInterview : TenantEntity
{
    public Guid EmployeeSeparationId { get; set; }
    public EmployeeSeparation? EmployeeSeparation { get; set; }

    public Guid InterviewerEmployeeId { get; set; }
    public Employee? InterviewerEmployee { get; set; }

    public DateTime InterviewDate { get; set; }
    public string? Feedback { get; set; }

    /// <summary>1-5, optional.</summary>
    public int? Rating { get; set; }
}

/// <summary>The final settlement calculation for a departing employee — last pay due,
/// leave encashment, minus any unsettled deductions (e.g. an advance never paid back).</summary>
public class FullAndFinalSettlement : SubmittableEntity
{
    public Guid EmployeeSeparationId { get; set; }
    public EmployeeSeparation? EmployeeSeparation { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public decimal TotalEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPayable { get; set; }
    public FnFStatus Status { get; set; } = FnFStatus.Draft;

    public ICollection<FnFComponent> Components { get; set; } = new List<FnFComponent>();
}

/// <summary>A line item on a Full & Final Settlement — reuses SalaryComponentType
/// (Earning/Deduction) from the Payroll module rather than duplicating the concept.</summary>
public class FnFComponent : TenantEntity
{
    public Guid FullAndFinalSettlementId { get; set; }
    public FullAndFinalSettlement? FullAndFinalSettlement { get; set; }

    public string Description { get; set; } = string.Empty;
    public SalaryComponentType ComponentType { get; set; }
    public decimal Amount { get; set; }
}
