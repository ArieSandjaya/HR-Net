using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Performance;
using HRMS.Domain.Enums;

namespace HRMS.Application.Performance;

public class AppraisalService : IAppraisalService
{
    private readonly IUnitOfWork _uow;

    public AppraisalService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<AppraisalDto>> GetByCycleAsync(Guid appraisalCycleId, CancellationToken ct = default)
    {
        var appraisals = await _uow.Repository<Appraisal>().FindAsync(a => a.AppraisalCycleId == appraisalCycleId, ct);
        var result = new List<AppraisalDto>();
        foreach (var a in appraisals)
            result.Add(await MapToDtoAsync(a, ct));

        return result;
    }

    public async Task<AppraisalDto?> GetByIdAsync(Guid appraisalId, CancellationToken ct = default)
    {
        var appraisal = await _uow.Repository<Appraisal>().GetByIdAsync(appraisalId, ct);
        return appraisal is null ? null : await MapToDtoAsync(appraisal, ct);
    }

    public async Task<Result<AppraisalDto>> CreateAsync(CreateAppraisalRequest request, CancellationToken ct = default)
    {
        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct);
        if (employee is null)
            return Result<AppraisalDto>.Failure("Pegawai tidak ditemukan.");

        var cycle = await _uow.Repository<AppraisalCycle>().GetByIdAsync(request.AppraisalCycleId, ct);
        if (cycle is null)
            return Result<AppraisalDto>.Failure("Siklus penilaian tidak ditemukan.");

        // BUG FIX: without this check, an employee from Company A could get an Appraisal
        // record under a cycle that belongs to Company B (the employee dropdown isn't
        // cross-filtered by company in the UI) — mixing performance data across companies.
        if (employee.CompanyId != cycle.CompanyId)
            return Result<AppraisalDto>.Failure("Pegawai ini bukan dari perusahaan yang sama dengan siklus penilaian.");

        var existing = await _uow.Repository<Appraisal>().FindAsync(
            a => a.EmployeeId == request.EmployeeId && a.AppraisalCycleId == request.AppraisalCycleId, ct);
        if (existing.Count > 0)
            return Result<AppraisalDto>.Failure("Pegawai ini sudah punya penilaian untuk siklus ini.");

        var appraisal = new Appraisal
        {
            EmployeeId = request.EmployeeId,
            AppraisalCycleId = request.AppraisalCycleId,
            Status = AppraisalStatus.Draft
        };

        await _uow.Repository<Appraisal>().AddAsync(appraisal, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<AppraisalDto>.Success(await MapToDtoAsync(appraisal, ct));
    }

    public async Task<Result> SubmitSelfAssessmentAsync(SubmitSelfAssessmentRequest request, CancellationToken ct = default)
    {
        if (request.SelfRating is < 1 or > 5)
            return Result.Failure("Rating diri harus antara 1-5.");

        var repo = _uow.Repository<Appraisal>();
        var appraisal = await repo.GetByIdAsync(request.AppraisalId, ct);
        if (appraisal is null)
            return Result.Failure("Penilaian tidak ditemukan.");
        if (appraisal.Status != AppraisalStatus.Draft)
            return Result.Failure("Self-assessment hanya bisa diisi sekali, saat status masih Draft.");

        appraisal.SelfAssessmentComments = request.Comments;
        appraisal.SelfRating = request.SelfRating;
        appraisal.Status = AppraisalStatus.SelfAssessmentSubmitted;

        repo.Update(appraisal);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SubmitManagerReviewAsync(SubmitManagerReviewRequest request, CancellationToken ct = default)
    {
        if (request.FinalRating is < 1 or > 5)
            return Result.Failure("Rating akhir harus antara 1-5.");

        var repo = _uow.Repository<Appraisal>();
        var appraisal = await repo.GetByIdAsync(request.AppraisalId, ct);
        if (appraisal is null)
            return Result.Failure("Penilaian tidak ditemukan.");
        if (appraisal.Status != AppraisalStatus.SelfAssessmentSubmitted)
            return Result.Failure("Pegawai belum mengisi self-assessment — review manager belum bisa dilakukan.");

        appraisal.ReviewedByEmployeeId = request.ReviewerEmployeeId;
        appraisal.ManagerComments = request.Comments;
        appraisal.FinalRating = request.FinalRating;
        appraisal.Status = AppraisalStatus.Completed;
        appraisal.DocStatus = DocumentStatus.Submitted;

        repo.Update(appraisal);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> AddFeedbackAsync(AddAppraisalFeedbackRequest request, CancellationToken ct = default)
    {
        if (request.Rating is < 1 or > 5)
            return Result.Failure("Rating harus antara 1-5.");

        var appraisal = await _uow.Repository<Appraisal>().GetByIdAsync(request.AppraisalId, ct);
        if (appraisal is null)
            return Result.Failure("Penilaian tidak ditemukan.");

        var reviewer = await _uow.Repository<Employee>().GetByIdAsync(request.ReviewerEmployeeId, ct);
        if (reviewer is null)
            return Result.Failure("Pemberi feedback tidak ditemukan.");

        await _uow.Repository<AppraisalFeedback>().AddAsync(new AppraisalFeedback
        {
            AppraisalId = request.AppraisalId,
            ReviewerEmployeeId = request.ReviewerEmployeeId,
            ReviewerRelation = request.ReviewerRelation,
            Rating = request.Rating,
            Comments = request.Comments
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<AppraisalDto> MapToDtoAsync(Appraisal a, CancellationToken ct)
    {
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeLookup = employees.ToDictionary(e => e.Id, e => e.FullName);
        var cycle = await _uow.Repository<AppraisalCycle>().GetByIdAsync(a.AppraisalCycleId, ct);

        var feedbacks = await _uow.Repository<AppraisalFeedback>().FindAsync(f => f.AppraisalId == a.Id, ct);
        var feedbackDtos = feedbacks.Select(f => new AppraisalFeedbackDto(
            f.Id, f.ReviewerEmployeeId, employeeLookup.GetValueOrDefault(f.ReviewerEmployeeId),
            f.ReviewerRelation, f.Rating, f.Comments)).ToList();

        var goals = await _uow.Repository<Goal>().FindAsync(g => g.EmployeeId == a.EmployeeId && g.AppraisalCycleId == a.AppraisalCycleId, ct);
        var kras = await _uow.Repository<KRA>().GetAllAsync(ct);
        var kraLookup = kras.ToDictionary(k => k.Id, k => k.Title);
        var goalDtos = goals.Select(g => new GoalDto(
            g.Id, g.EmployeeId, employeeLookup.GetValueOrDefault(g.EmployeeId), g.AppraisalCycleId, cycle?.Name,
            g.KRAId, kraLookup.GetValueOrDefault(g.KRAId), g.Description, g.ProgressPercent, g.Status)).ToList();

        return new AppraisalDto(
            a.Id, a.EmployeeId, employeeLookup.GetValueOrDefault(a.EmployeeId), a.AppraisalCycleId, cycle?.Name,
            a.Status, a.SelfAssessmentComments, a.SelfRating,
            a.ReviewedByEmployeeId, a.ReviewedByEmployeeId.HasValue ? employeeLookup.GetValueOrDefault(a.ReviewedByEmployeeId.Value) : null,
            a.ManagerComments, a.FinalRating, feedbackDtos, goalDtos);
    }
}
