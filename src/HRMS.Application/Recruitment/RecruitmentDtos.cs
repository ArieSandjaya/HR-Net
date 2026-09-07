using HRMS.Domain.Enums;

namespace HRMS.Application.Recruitment;

// ---- Job Opening ----
public record JobOpeningDto(
    Guid Id, string JobTitle, Guid DepartmentId, string? DepartmentName,
    Guid DesignationId, string? DesignationName, string? Description,
    int NumberOfPositions, DateOnly PostedDate, DateOnly? ClosesOn,
    JobOpeningStatus Status, int ApplicantCount
);

public record CreateJobOpeningRequest(
    string JobTitle, Guid DepartmentId, Guid DesignationId,
    string? Description, int NumberOfPositions, DateOnly? ClosesOn
);

// ---- Job Applicant ----
public record JobApplicantDto(
    Guid Id, string ApplicantName, string Email, string? Phone, string? ResumeNotes,
    Guid JobOpeningId, string? JobOpeningTitle, DateOnly AppliedDate, string? Source,
    JobApplicantStatus Status
);

public record CreateJobApplicantRequest(
    string ApplicantName, string Email, string? Phone, string? ResumeNotes,
    Guid JobOpeningId, string? Source
);

// ---- Interview ----
public record InterviewDto(
    Guid Id, Guid JobApplicantId, string? ApplicantName, string InterviewRound,
    DateTime ScheduledOn, InterviewStatus Status,
    Guid? InterviewerEmployeeId, string? InterviewerName,
    List<InterviewFeedbackDto> Feedbacks
);

public record InterviewFeedbackDto(
    Guid Id, Guid InterviewerEmployeeId, string? InterviewerName,
    int Rating, string? Comments, bool Recommended
);

public record ScheduleInterviewRequest(
    Guid JobApplicantId, string InterviewRound, DateTime ScheduledOn, Guid? InterviewerEmployeeId
);

public record SubmitInterviewFeedbackRequest(
    Guid InterviewId, Guid InterviewerEmployeeId, int Rating, string? Comments, bool Recommended
);

// ---- Job Offer ----
public record JobOfferDto(
    Guid Id, Guid JobApplicantId, string? ApplicantName, Guid DesignationId, string? DesignationName,
    Guid DepartmentId, string? DepartmentName, DateOnly OfferDate, DateOnly? ExpectedJoiningDate,
    decimal? AnnualCtc, JobOfferStatus Status, Guid? CreatedEmployeeId
);

public record CreateJobOfferRequest(
    Guid JobApplicantId, Guid DesignationId, Guid DepartmentId,
    DateOnly? ExpectedJoiningDate, decimal? AnnualCtc
);
