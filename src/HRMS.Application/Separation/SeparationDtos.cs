using HRMS.Domain.Enums;

namespace HRMS.Application.Separation;

// ---- Employee Separation ----
public record EmployeeSeparationDto(
    Guid Id, Guid EmployeeId, string? EmployeeName, DateOnly ResignationDate, DateOnly? RelievingDate,
    string? Reason, SeparationType SeparationType, SeparationStatus Status,
    Guid? ApprovedByEmployeeId, string? ApprovedByName
);

public record CreateEmployeeSeparationRequest(Guid EmployeeId, DateOnly ResignationDate, string? Reason, SeparationType SeparationType);
public record ApproveSeparationRequest(Guid SeparationId, Guid ApproverEmployeeId, DateOnly RelievingDate);

// ---- Exit Interview ----
public record ExitInterviewDto(
    Guid Id, Guid EmployeeSeparationId, Guid InterviewerEmployeeId, string? InterviewerName,
    DateTime InterviewDate, string? Feedback, int? Rating
);

public record RecordExitInterviewRequest(Guid EmployeeSeparationId, Guid InterviewerEmployeeId, DateTime InterviewDate, string? Feedback, int? Rating);

// ---- Full & Final Settlement ----
public record FnFComponentDto(Guid Id, string Description, SalaryComponentType ComponentType, decimal Amount);

public record FullAndFinalSettlementDto(
    Guid Id, Guid EmployeeSeparationId, Guid EmployeeId, string? EmployeeName,
    decimal TotalEarnings, decimal TotalDeductions, decimal NetPayable, FnFStatus Status,
    List<FnFComponentDto> Components
);

public record AddFnFComponentRequest(Guid FullAndFinalSettlementId, string Description, SalaryComponentType ComponentType, decimal Amount);
