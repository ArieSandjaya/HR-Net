using HRMS.Application.Common.Models;

namespace HRMS.Application.Performance;

public interface IAppraisalCycleService
{
    Task<List<AppraisalCycleDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<AppraisalCycleDto>> CreateAsync(CreateAppraisalCycleRequest request, CancellationToken ct = default);
    Task<Result> ActivateAsync(Guid cycleId, CancellationToken ct = default);
    Task<Result> CompleteAsync(Guid cycleId, CancellationToken ct = default);
}

public interface IKraService
{
    Task<List<KraDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<KraDto>> CreateAsync(CreateKraRequest request, CancellationToken ct = default);
}

public interface IGoalService
{
    Task<List<GoalDto>> GetByEmployeeAndCycleAsync(Guid employeeId, Guid appraisalCycleId, CancellationToken ct = default);
    Task<Result<GoalDto>> CreateAsync(CreateGoalRequest request, CancellationToken ct = default);
    Task<Result> UpdateProgressAsync(UpdateGoalProgressRequest request, CancellationToken ct = default);
}

public interface IAppraisalService
{
    Task<List<AppraisalDto>> GetByCycleAsync(Guid appraisalCycleId, CancellationToken ct = default);
    Task<AppraisalDto?> GetByIdAsync(Guid appraisalId, CancellationToken ct = default);
    Task<Result<AppraisalDto>> CreateAsync(CreateAppraisalRequest request, CancellationToken ct = default);
    Task<Result> SubmitSelfAssessmentAsync(SubmitSelfAssessmentRequest request, CancellationToken ct = default);
    Task<Result> SubmitManagerReviewAsync(SubmitManagerReviewRequest request, CancellationToken ct = default);
    Task<Result> AddFeedbackAsync(AddAppraisalFeedbackRequest request, CancellationToken ct = default);
}
