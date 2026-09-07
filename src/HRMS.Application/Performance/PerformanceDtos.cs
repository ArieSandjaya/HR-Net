using HRMS.Domain.Enums;

namespace HRMS.Application.Performance;

// ---- Appraisal Cycle ----
public record AppraisalCycleDto(
    Guid Id, string Name, Guid CompanyId, string? CompanyName,
    DateOnly StartDate, DateOnly EndDate, AppraisalCycleStatus Status
);

public record CreateAppraisalCycleRequest(string Name, Guid CompanyId, DateOnly StartDate, DateOnly EndDate);

// ---- KRA ----
public record KraDto(Guid Id, string Title, string? Description);
public record CreateKraRequest(string Title, string? Description);

// ---- Goal ----
public record GoalDto(
    Guid Id, Guid EmployeeId, string? EmployeeName, Guid AppraisalCycleId, string? CycleName,
    Guid KRAId, string? KraTitle, string Description, decimal ProgressPercent, GoalStatus Status
);

public record CreateGoalRequest(Guid EmployeeId, Guid AppraisalCycleId, Guid KRAId, string Description);
public record UpdateGoalProgressRequest(Guid GoalId, decimal ProgressPercent);

// ---- Appraisal ----
public record AppraisalFeedbackDto(
    Guid Id, Guid ReviewerEmployeeId, string? ReviewerName, FeedbackReviewerRelation ReviewerRelation,
    decimal Rating, string? Comments
);

public record AppraisalDto(
    Guid Id, Guid EmployeeId, string? EmployeeName, Guid AppraisalCycleId, string? CycleName,
    AppraisalStatus Status, string? SelfAssessmentComments, decimal? SelfRating,
    Guid? ReviewedByEmployeeId, string? ReviewedByName, string? ManagerComments, decimal? FinalRating,
    List<AppraisalFeedbackDto> Feedbacks, List<GoalDto> Goals
);

public record CreateAppraisalRequest(Guid EmployeeId, Guid AppraisalCycleId);
public record SubmitSelfAssessmentRequest(Guid AppraisalId, string Comments, decimal SelfRating);
public record SubmitManagerReviewRequest(Guid AppraisalId, Guid ReviewerEmployeeId, string Comments, decimal FinalRating);
public record AddAppraisalFeedbackRequest(
    Guid AppraisalId, Guid ReviewerEmployeeId, FeedbackReviewerRelation ReviewerRelation, decimal Rating, string? Comments
);
