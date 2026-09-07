using HRMS.Application.Common.Models;

namespace HRMS.Application.Recruitment;

public interface IJobOpeningService
{
    Task<List<JobOpeningDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<JobOpeningDto>> CreateAsync(CreateJobOpeningRequest request, CancellationToken ct = default);
    Task<Result> CloseAsync(Guid jobOpeningId, CancellationToken ct = default);
}

public interface IJobApplicantService
{
    Task<List<JobApplicantDto>> GetByJobOpeningAsync(Guid jobOpeningId, CancellationToken ct = default);
    Task<List<JobApplicantDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<JobApplicantDto>> CreateAsync(CreateJobApplicantRequest request, CancellationToken ct = default);
    Task<Result> UpdateStatusAsync(Guid jobApplicantId, HRMS.Domain.Enums.JobApplicantStatus status, CancellationToken ct = default);
}

public interface IInterviewService
{
    Task<List<InterviewDto>> GetByApplicantAsync(Guid jobApplicantId, CancellationToken ct = default);
    Task<Result<InterviewDto>> ScheduleAsync(ScheduleInterviewRequest request, CancellationToken ct = default);
    Task<Result> SubmitFeedbackAsync(SubmitInterviewFeedbackRequest request, CancellationToken ct = default);
    Task<Result> UpdateStatusAsync(Guid interviewId, HRMS.Domain.Enums.InterviewStatus status, CancellationToken ct = default);
}

public interface IJobOfferService
{
    Task<List<JobOfferDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<JobOfferDto>> CreateAsync(CreateJobOfferRequest request, CancellationToken ct = default);
    Task<Result> RespondAsync(Guid jobOfferId, bool accepted, CancellationToken ct = default);

    /// <summary>Converts an accepted offer into an actual Employee record — the bridge back
    /// into the Employee module that closes out the recruitment pipeline. Company is derived
    /// automatically from the offer's Department (see JobOfferService for why).</summary>
    Task<Result<Guid>> ConvertToEmployeeAsync(Guid jobOfferId, Guid employmentTypeId, DateOnly dateOfJoining, CancellationToken ct = default);
}
