using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Performance;
using HRMS.Domain.Enums;

namespace HRMS.Application.Performance;

public class GoalService : IGoalService
{
    private readonly IUnitOfWork _uow;

    public GoalService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<GoalDto>> GetByEmployeeAndCycleAsync(Guid employeeId, Guid appraisalCycleId, CancellationToken ct = default)
    {
        var goals = await _uow.Repository<Goal>().FindAsync(
            g => g.EmployeeId == employeeId && g.AppraisalCycleId == appraisalCycleId, ct);

        var employee = await _uow.Repository<Employee>().GetByIdAsync(employeeId, ct);
        var cycle = await _uow.Repository<AppraisalCycle>().GetByIdAsync(appraisalCycleId, ct);
        var kras = await _uow.Repository<KRA>().GetAllAsync(ct);
        var kraLookup = kras.ToDictionary(k => k.Id, k => k.Title);

        return goals.Select(g => new GoalDto(
            g.Id, g.EmployeeId, employee?.FullName, g.AppraisalCycleId, cycle?.Name,
            g.KRAId, kraLookup.GetValueOrDefault(g.KRAId), g.Description, g.ProgressPercent, g.Status))
            .ToList();
    }

    public async Task<Result<GoalDto>> CreateAsync(CreateGoalRequest request, CancellationToken ct = default)
    {
        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct);
        if (employee is null)
            return Result<GoalDto>.Failure("Pegawai tidak ditemukan.");

        var cycle = await _uow.Repository<AppraisalCycle>().GetByIdAsync(request.AppraisalCycleId, ct);
        if (cycle is null)
            return Result<GoalDto>.Failure("Siklus penilaian tidak ditemukan.");

        if (employee.CompanyId != cycle.CompanyId)
            return Result<GoalDto>.Failure("Pegawai ini bukan dari perusahaan yang sama dengan siklus penilaian.");

        var kra = await _uow.Repository<KRA>().GetByIdAsync(request.KRAId, ct);
        if (kra is null)
            return Result<GoalDto>.Failure("KRA tidak ditemukan.");

        if (string.IsNullOrWhiteSpace(request.Description))
            return Result<GoalDto>.Failure("Deskripsi goal tidak boleh kosong.");

        var goal = new Goal
        {
            EmployeeId = request.EmployeeId,
            AppraisalCycleId = request.AppraisalCycleId,
            KRAId = request.KRAId,
            Description = request.Description,
            ProgressPercent = 0,
            Status = GoalStatus.Pending
        };

        await _uow.Repository<Goal>().AddAsync(goal, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<GoalDto>.Success(new GoalDto(
            goal.Id, goal.EmployeeId, employee.FullName, goal.AppraisalCycleId, cycle.Name,
            goal.KRAId, kra.Title, goal.Description, goal.ProgressPercent, goal.Status));
    }

    public async Task<Result> UpdateProgressAsync(UpdateGoalProgressRequest request, CancellationToken ct = default)
    {
        if (request.ProgressPercent is < 0 or > 100)
            return Result.Failure("Progress harus antara 0-100.");

        var repo = _uow.Repository<Goal>();
        var goal = await repo.GetByIdAsync(request.GoalId, ct);
        if (goal is null)
            return Result.Failure("Goal tidak ditemukan.");

        goal.ProgressPercent = request.ProgressPercent;
        goal.Status = request.ProgressPercent >= 100
            ? GoalStatus.Completed
            : request.ProgressPercent > 0
                ? GoalStatus.InProgress
                : GoalStatus.Pending;

        repo.Update(goal);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
