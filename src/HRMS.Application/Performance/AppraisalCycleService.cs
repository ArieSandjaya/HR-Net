using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Entities.Performance;
using HRMS.Domain.Enums;

namespace HRMS.Application.Performance;

public class AppraisalCycleService : IAppraisalCycleService
{
    private readonly IUnitOfWork _uow;

    public AppraisalCycleService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<AppraisalCycleDto>> GetAllAsync(CancellationToken ct = default)
    {
        var cycles = await _uow.Repository<AppraisalCycle>().GetAllAsync(ct);
        var companies = await _uow.Repository<Company>().GetAllAsync(ct);
        var companyNames = companies.ToDictionary(c => c.Id, c => c.Name);

        return cycles
            .OrderByDescending(c => c.StartDate)
            .Select(c => new AppraisalCycleDto(c.Id, c.Name, c.CompanyId, companyNames.GetValueOrDefault(c.CompanyId), c.StartDate, c.EndDate, c.Status))
            .ToList();
    }

    public async Task<Result<AppraisalCycleDto>> CreateAsync(CreateAppraisalCycleRequest request, CancellationToken ct = default)
    {
        if (request.EndDate < request.StartDate)
            return Result<AppraisalCycleDto>.Failure("Tanggal selesai tidak boleh sebelum tanggal mulai.");

        var company = await _uow.Repository<Company>().GetByIdAsync(request.CompanyId, ct);
        if (company is null)
            return Result<AppraisalCycleDto>.Failure("Perusahaan tidak ditemukan.");

        var cycle = new AppraisalCycle
        {
            Name = request.Name,
            CompanyId = request.CompanyId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = AppraisalCycleStatus.Draft
        };

        await _uow.Repository<AppraisalCycle>().AddAsync(cycle, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<AppraisalCycleDto>.Success(new AppraisalCycleDto(cycle.Id, cycle.Name, cycle.CompanyId, company.Name, cycle.StartDate, cycle.EndDate, cycle.Status));
    }

    public async Task<Result> ActivateAsync(Guid cycleId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<AppraisalCycle>();
        var cycle = await repo.GetByIdAsync(cycleId, ct);
        if (cycle is null)
            return Result.Failure("Siklus penilaian tidak ditemukan.");

        cycle.Status = AppraisalCycleStatus.Active;
        repo.Update(cycle);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> CompleteAsync(Guid cycleId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<AppraisalCycle>();
        var cycle = await repo.GetByIdAsync(cycleId, ct);
        if (cycle is null)
            return Result.Failure("Siklus penilaian tidak ditemukan.");

        cycle.Status = AppraisalCycleStatus.Completed;
        repo.Update(cycle);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class KraService : IKraService
{
    private readonly IUnitOfWork _uow;

    public KraService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<KraDto>> GetAllAsync(CancellationToken ct = default)
    {
        var kras = await _uow.Repository<KRA>().GetAllAsync(ct);
        return kras.Select(k => new KraDto(k.Id, k.Title, k.Description)).ToList();
    }

    public async Task<Result<KraDto>> CreateAsync(CreateKraRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Result<KraDto>.Failure("Judul KRA tidak boleh kosong.");

        var kra = new KRA { Title = request.Title, Description = request.Description };
        await _uow.Repository<KRA>().AddAsync(kra, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<KraDto>.Success(new KraDto(kra.Id, kra.Title, kra.Description));
    }
}
