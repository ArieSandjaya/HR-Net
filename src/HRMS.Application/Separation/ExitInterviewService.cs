using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Separation;

namespace HRMS.Application.Separation;

public class ExitInterviewService : IExitInterviewService
{
    private readonly IUnitOfWork _uow;

    public ExitInterviewService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<ExitInterviewDto>> GetBySeparationAsync(Guid separationId, CancellationToken ct = default)
    {
        var interviews = await _uow.Repository<ExitInterview>().FindAsync(i => i.EmployeeSeparationId == separationId, ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);

        return interviews
            .OrderByDescending(i => i.InterviewDate)
            .Select(i => new ExitInterviewDto(
                i.Id, i.EmployeeSeparationId, i.InterviewerEmployeeId, employeeNames.GetValueOrDefault(i.InterviewerEmployeeId),
                i.InterviewDate, i.Feedback, i.Rating))
            .ToList();
    }

    public async Task<Result<ExitInterviewDto>> RecordAsync(RecordExitInterviewRequest request, CancellationToken ct = default)
    {
        if (request.Rating is < 1 or > 5)
            return Result<ExitInterviewDto>.Failure("Rating harus antara 1-5.");

        var separation = await _uow.Repository<EmployeeSeparation>().GetByIdAsync(request.EmployeeSeparationId, ct);
        if (separation is null)
            return Result<ExitInterviewDto>.Failure("Data separation tidak ditemukan.");

        var interviewer = await _uow.Repository<Employee>().GetByIdAsync(request.InterviewerEmployeeId, ct);
        if (interviewer is null)
            return Result<ExitInterviewDto>.Failure("Pewawancara tidak ditemukan.");

        var interview = new ExitInterview
        {
            EmployeeSeparationId = request.EmployeeSeparationId,
            InterviewerEmployeeId = request.InterviewerEmployeeId,
            InterviewDate = request.InterviewDate,
            Feedback = request.Feedback,
            Rating = request.Rating
        };

        await _uow.Repository<ExitInterview>().AddAsync(interview, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ExitInterviewDto>.Success(new ExitInterviewDto(
            interview.Id, interview.EmployeeSeparationId, interview.InterviewerEmployeeId, interviewer.FullName,
            interview.InterviewDate, interview.Feedback, interview.Rating));
    }
}
