using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Recruitment;

public class JobOpening : TenantEntity
{
    public string JobTitle { get; set; } = string.Empty;

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid DesignationId { get; set; }
    public Designation? Designation { get; set; }

    public string? Description { get; set; }
    public int NumberOfPositions { get; set; } = 1;

    public DateOnly PostedDate { get; set; }
    public DateOnly? ClosesOn { get; set; }
    public JobOpeningStatus Status { get; set; } = JobOpeningStatus.Open;

    public ICollection<JobApplicant> Applicants { get; set; } = new List<JobApplicant>();
}

public class JobApplicant : TenantEntity
{
    public string ApplicantName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }

    /// <summary>Free-text notes/link for now — a proper resume file upload can be added
    /// once the file-storage module exists (see README roadmap).</summary>
    public string? ResumeNotes { get; set; }

    public Guid JobOpeningId { get; set; }
    public JobOpening? JobOpening { get; set; }

    public DateOnly AppliedDate { get; set; }
    public string? Source { get; set; }
    public JobApplicantStatus Status { get; set; } = JobApplicantStatus.Open;
}

public class Interview : TenantEntity
{
    public Guid JobApplicantId { get; set; }
    public JobApplicant? JobApplicant { get; set; }

    public string InterviewRound { get; set; } = "Round 1";
    public DateTime ScheduledOn { get; set; }
    public InterviewStatus Status { get; set; } = InterviewStatus.Pending;

    public Guid? InterviewerEmployeeId { get; set; }
    public Employee? InterviewerEmployee { get; set; }

    public ICollection<InterviewFeedback> Feedbacks { get; set; } = new List<InterviewFeedback>();
}

public class InterviewFeedback : TenantEntity
{
    public Guid InterviewId { get; set; }
    public Interview? Interview { get; set; }

    public Guid InterviewerEmployeeId { get; set; }
    public Employee? InterviewerEmployee { get; set; }

    /// <summary>1 (poor) to 5 (excellent).</summary>
    public int Rating { get; set; }
    public string? Comments { get; set; }
    public bool Recommended { get; set; }
}

/// <summary>Goes through a Draft -> Submitted workflow (via SubmittableEntity) since an offer
/// is a formal document once sent, similar to LeaveApplication/AttendanceRecord.</summary>
public class JobOffer : SubmittableEntity
{
    public Guid JobApplicantId { get; set; }
    public JobApplicant? JobApplicant { get; set; }

    public Guid DesignationId { get; set; }
    public Designation? Designation { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public DateOnly OfferDate { get; set; }
    public DateOnly? ExpectedJoiningDate { get; set; }
    public decimal? AnnualCtc { get; set; }

    public JobOfferStatus Status { get; set; } = JobOfferStatus.AwaitingResponse;

    /// <summary>Set once the offer is accepted and converted into an actual Employee record
    /// (see JobOfferService.ConvertToEmployeeAsync) — links recruitment history to the hire.</summary>
    public Guid? CreatedEmployeeId { get; set; }
}
