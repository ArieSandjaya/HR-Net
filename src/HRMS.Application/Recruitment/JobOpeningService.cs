using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Domain.Enums;

namespace HRMS.Application.Recruitment;

public class JobOpeningService : IJobOpeningService
{
    private readonly IUnitOfWork _uow;

    public JobOpeningService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<JobOpeningDto>> GetAllAsync(CancellationToken ct = default)
    {
        var openings = await _uow.Repository<JobOpening>().GetAllAsync(ct);
        var departments = await _uow.Repository<Department>().GetAllAsync(ct);
        var designations = await _uow.Repository<Designation>().GetAllAsync(ct);
        var applicants = await _uow.Repository<JobApplicant>().GetAllAsync(ct);

        var deptNames = departments.ToDictionary(d => d.Id, d => d.Name);
        var desigNames = designations.ToDictionary(d => d.Id, d => d.Name);
        var applicantCounts = applicants.GroupBy(a => a.JobOpeningId).ToDictionary(g => g.Key, g => g.Count());

        return openings
            .OrderByDescending(o => o.PostedDate)
            .Select(o => new JobOpeningDto(
                o.Id, o.JobTitle, o.DepartmentId, deptNames.GetValueOrDefault(o.DepartmentId),
                o.DesignationId, desigNames.GetValueOrDefault(o.DesignationId), o.Description,
                o.NumberOfPositions, o.PostedDate, o.ClosesOn, o.Status,
                applicantCounts.GetValueOrDefault(o.Id)))
            .ToList();
    }

    public async Task<Result<JobOpeningDto>> CreateAsync(CreateJobOpeningRequest request, CancellationToken ct = default)
    {
        if (request.ClosesOn.HasValue && request.ClosesOn < DateOnly.FromDateTime(DateTime.Today))
            return Result<JobOpeningDto>.Failure("Tanggal tutup lowongan tidak boleh di masa lalu.");

        var opening = new JobOpening
        {
            JobTitle = request.JobTitle,
            DepartmentId = request.DepartmentId,
            DesignationId = request.DesignationId,
            Description = request.Description,
            NumberOfPositions = request.NumberOfPositions,
            PostedDate = DateOnly.FromDateTime(DateTime.Today),
            ClosesOn = request.ClosesOn,
            Status = JobOpeningStatus.Open
        };

        await _uow.Repository<JobOpening>().AddAsync(opening, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<JobOpeningDto>.Success(new JobOpeningDto(
            opening.Id, opening.JobTitle, opening.DepartmentId, null, opening.DesignationId, null,
            opening.Description, opening.NumberOfPositions, opening.PostedDate, opening.ClosesOn,
            opening.Status, 0));
    }

    public async Task<Result> CloseAsync(Guid jobOpeningId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<JobOpening>();
        var opening = await repo.GetByIdAsync(jobOpeningId, ct);
        if (opening is null)
            return Result.Failure("Lowongan tidak ditemukan.");

        opening.Status = JobOpeningStatus.Closed;
        repo.Update(opening);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
