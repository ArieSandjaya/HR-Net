using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Domain.Enums;

namespace HRMS.Application.Recruitment;

public class InterviewService : IInterviewService
{
    private readonly IUnitOfWork _uow;

    public InterviewService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<InterviewDto>> GetByApplicantAsync(Guid jobApplicantId, CancellationToken ct = default)
    {
        var interviews = await _uow.Repository<Interview>().FindAsync(i => i.JobApplicantId == jobApplicantId, ct);
        var applicant = await _uow.Repository<JobApplicant>().GetByIdAsync(jobApplicantId, ct);
        var feedbacks = await _uow.Repository<InterviewFeedback>().FindAsync(f => interviews.Select(i => i.Id).Contains(f.InterviewId), ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);

        return interviews
            .OrderByDescending(i => i.ScheduledOn)
            .Select(i => MapToDto(i, applicant?.ApplicantName, feedbacks.Where(f => f.InterviewId == i.Id).ToList(), employeeNames))
            .ToList();
    }

    public async Task<Result<InterviewDto>> ScheduleAsync(ScheduleInterviewRequest request, CancellationToken ct = default)
    {
        var applicant = await _uow.Repository<JobApplicant>().GetByIdAsync(request.JobApplicantId, ct);
        if (applicant is null)
            return Result<InterviewDto>.Failure("Pelamar tidak ditemukan.");
        if (applicant.Status is JobApplicantStatus.Rejected or JobApplicantStatus.Accepted)
            return Result<InterviewDto>.Failure($"Tidak bisa menjadwalkan interview — status pelamar sudah final ({applicant.Status}).");

        var interview = new Interview
        {
            JobApplicantId = request.JobApplicantId,
            InterviewRound = request.InterviewRound,
            ScheduledOn = request.ScheduledOn,
            InterviewerEmployeeId = request.InterviewerEmployeeId,
            Status = InterviewStatus.Pending
        };

        await _uow.Repository<Interview>().AddAsync(interview, ct);

        // Scheduling an interview naturally moves the applicant into "InterviewScheduled".
        applicant.Status = JobApplicantStatus.InterviewScheduled;
        _uow.Repository<JobApplicant>().Update(applicant);

        await _uow.SaveChangesAsync(ct);

        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);

        return Result<InterviewDto>.Success(MapToDto(interview, applicant.ApplicantName, new List<InterviewFeedback>(), employeeNames));
    }

    public async Task<Result> SubmitFeedbackAsync(SubmitInterviewFeedbackRequest request, CancellationToken ct = default)
    {
        if (request.Rating is < 1 or > 5)
            return Result.Failure("Rating harus antara 1 sampai 5.");

        var interview = await _uow.Repository<Interview>().GetByIdAsync(request.InterviewId, ct);
        if (interview is null)
            return Result.Failure("Jadwal interview tidak ditemukan.");

        await _uow.Repository<InterviewFeedback>().AddAsync(new InterviewFeedback
        {
            InterviewId = request.InterviewId,
            InterviewerEmployeeId = request.InterviewerEmployeeId,
            Rating = request.Rating,
            Comments = request.Comments,
            Recommended = request.Recommended
        }, ct);

        interview.Status = request.Recommended ? InterviewStatus.Cleared : InterviewStatus.Rejected;
        _uow.Repository<Interview>().Update(interview);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> UpdateStatusAsync(Guid interviewId, InterviewStatus status, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Interview>();
        var interview = await repo.GetByIdAsync(interviewId, ct);
        if (interview is null)
            return Result.Failure("Jadwal interview tidak ditemukan.");

        interview.Status = status;
        repo.Update(interview);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static InterviewDto MapToDto(Interview i, string? applicantName, List<InterviewFeedback> feedbacks, Dictionary<Guid, string> employeeNames) => new(
        i.Id, i.JobApplicantId, applicantName, i.InterviewRound, i.ScheduledOn, i.Status,
        i.InterviewerEmployeeId, i.InterviewerEmployeeId.HasValue ? employeeNames.GetValueOrDefault(i.InterviewerEmployeeId.Value) : null,
        feedbacks.Select(f => new InterviewFeedbackDto(
            f.Id, f.InterviewerEmployeeId, employeeNames.GetValueOrDefault(f.InterviewerEmployeeId),
            f.Rating, f.Comments, f.Recommended)).ToList());
}
