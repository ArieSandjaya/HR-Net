using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Performance;

/// <summary>A review period (e.g. "Semester 1 2026"). Goals and Appraisals are always
/// scoped to one cycle.</summary>
public class AppraisalCycle : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public AppraisalCycleStatus Status { get; set; } = AppraisalCycleStatus.Draft;
}

/// <summary>Key Result Area — a reusable goal category (e.g. "Kualitas Kerja",
/// "Pencapaian Target Penjualan"), shared across cycles.</summary>
public class KRA : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
}

/// <summary>An individual, measurable goal set for one employee within one cycle.</summary>
public class Goal : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid AppraisalCycleId { get; set; }
    public AppraisalCycle? AppraisalCycle { get; set; }

    public Guid KRAId { get; set; }
    public KRA? KRA { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>0-100.</summary>
    public decimal ProgressPercent { get; set; }
    public GoalStatus Status { get; set; } = GoalStatus.Pending;
}

/// <summary>The performance review record for one employee in one cycle — aggregates
/// self-assessment, manager review, and a final rating. Goes through a workflow similar to
/// other document entities (SubmittableEntity), plus a more specific AppraisalStatus for the
/// self-assessment/manager-review stages.</summary>
public class Appraisal : SubmittableEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid AppraisalCycleId { get; set; }
    public AppraisalCycle? AppraisalCycle { get; set; }

    public AppraisalStatus Status { get; set; } = AppraisalStatus.Draft;

    public string? SelfAssessmentComments { get; set; }
    /// <summary>1-5, filled in by the employee.</summary>
    public decimal? SelfRating { get; set; }

    public Guid? ReviewedByEmployeeId { get; set; }
    public Employee? ReviewedBy { get; set; }
    public string? ManagerComments { get; set; }
    /// <summary>1-5, the final rating recorded by the reviewing manager.</summary>
    public decimal? FinalRating { get; set; }

    public ICollection<AppraisalFeedback> Feedbacks { get; set; } = new List<AppraisalFeedback>();
}

/// <summary>Optional 360-style feedback from a peer/manager/self on an Appraisal —
/// same shape as Recruitment's InterviewFeedback, reused conceptually here.</summary>
public class AppraisalFeedback : TenantEntity
{
    public Guid AppraisalId { get; set; }
    public Appraisal? Appraisal { get; set; }

    public Guid ReviewerEmployeeId { get; set; }
    public Employee? ReviewerEmployee { get; set; }
    public FeedbackReviewerRelation ReviewerRelation { get; set; }

    /// <summary>1-5.</summary>
    public decimal Rating { get; set; }
    public string? Comments { get; set; }
}
