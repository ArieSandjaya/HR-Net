using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Domain.Enums;

namespace HRMS.Application.Recruitment;

public class JobApplicantService : IJobApplicantService
{
    private readonly IUnitOfWork _uow;

    public JobApplicantService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<JobApplicantDto>> GetByJobOpeningAsync(Guid jobOpeningId, CancellationToken ct = default)
    {
        var applicants = await _uow.Repository<JobApplicant>().FindAsync(a => a.JobOpeningId == jobOpeningId, ct);
        var opening = await _uow.Repository<JobOpening>().GetByIdAsync(jobOpeningId, ct);
        return applicants.OrderByDescending(a => a.AppliedDate).Select(a => MapToDto(a, opening?.JobTitle)).ToList();
    }

    public async Task<List<JobApplicantDto>> GetAllAsync(CancellationToken ct = default)
    {
        var applicants = await _uow.Repository<JobApplicant>().GetAllAsync(ct);
        var openings = await _uow.Repository<JobOpening>().GetAllAsync(ct);
        var openingTitles = openings.ToDictionary(o => o.Id, o => o.JobTitle);

        return applicants
            .OrderByDescending(a => a.AppliedDate)
            .Select(a => MapToDto(a, openingTitles.GetValueOrDefault(a.JobOpeningId)))
            .ToList();
    }

    public async Task<Result<JobApplicantDto>> CreateAsync(CreateJobApplicantRequest request, CancellationToken ct = default)
    {
        var opening = await _uow.Repository<JobOpening>().GetByIdAsync(request.JobOpeningId, ct);
        if (opening is null)
            return Result<JobApplicantDto>.Failure("Lowongan tidak ditemukan.");
        if (opening.Status != JobOpeningStatus.Open)
            return Result<JobApplicantDto>.Failure("Lowongan ini sudah ditutup, tidak bisa menerima pelamar baru.");

        var applicant = new JobApplicant
        {
            ApplicantName = request.ApplicantName,
            Email = request.Email,
            Phone = request.Phone,
            ResumeNotes = request.ResumeNotes,
            JobOpeningId = request.JobOpeningId,
            Source = request.Source,
            AppliedDate = DateOnly.FromDateTime(DateTime.Today),
            Status = JobApplicantStatus.Open
        };

        await _uow.Repository<JobApplicant>().AddAsync(applicant, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<JobApplicantDto>.Success(MapToDto(applicant, opening.JobTitle));
    }

    public async Task<Result> UpdateStatusAsync(Guid jobApplicantId, JobApplicantStatus status, CancellationToken ct = default)
    {
        var repo = _uow.Repository<JobApplicant>();
        var applicant = await repo.GetByIdAsync(jobApplicantId, ct);
        if (applicant is null)
            return Result.Failure("Pelamar tidak ditemukan.");

        applicant.Status = status;
        repo.Update(applicant);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static JobApplicantDto MapToDto(JobApplicant a, string? jobOpeningTitle) => new(
        a.Id, a.ApplicantName, a.Email, a.Phone, a.ResumeNotes,
        a.JobOpeningId, jobOpeningTitle, a.AppliedDate, a.Source, a.Status);
}
