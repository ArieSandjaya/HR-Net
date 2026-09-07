using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Entities.Payroll;

namespace HRMS.Application.Payroll;

public class SalaryStructureService : ISalaryStructureService
{
    private readonly IUnitOfWork _uow;

    public SalaryStructureService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<SalaryStructureDto>> GetAllAsync(CancellationToken ct = default)
    {
        var structures = await _uow.Repository<SalaryStructure>().GetAllAsync(ct);
        var companies = await _uow.Repository<Company>().GetAllAsync(ct);
        var companyNames = companies.ToDictionary(c => c.Id, c => c.Name);

        var result = new List<SalaryStructureDto>();
        foreach (var s in structures)
            result.Add(await MapToDtoAsync(s, companyNames.GetValueOrDefault(s.CompanyId), ct));

        return result;
    }

    public async Task<SalaryStructureDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var structure = await _uow.Repository<SalaryStructure>().GetByIdAsync(id, ct);
        if (structure is null) return null;

        var company = await _uow.Repository<Company>().GetByIdAsync(structure.CompanyId, ct);
        return await MapToDtoAsync(structure, company?.Name, ct);
    }

    public async Task<Result<SalaryStructureDto>> CreateAsync(CreateSalaryStructureRequest request, CancellationToken ct = default)
    {
        var company = await _uow.Repository<Company>().GetByIdAsync(request.CompanyId, ct);
        if (company is null)
            return Result<SalaryStructureDto>.Failure("Perusahaan tidak ditemukan.");

        var structure = new SalaryStructure { Name = request.Name, CompanyId = request.CompanyId, IsActive = true };
        await _uow.Repository<SalaryStructure>().AddAsync(structure, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<SalaryStructureDto>.Success(await MapToDtoAsync(structure, company.Name, ct));
    }

    public async Task<Result> AddComponentAsync(AddStructureComponentRequest request, CancellationToken ct = default)
    {
        if (request.AmountOverride is < 0)
            return Result.Failure("Nominal override tidak boleh negatif.");
        if (request.PercentageOverride is < 0 or > 100)
            return Result.Failure("Persentase override harus antara 0-100.");

        var structure = await _uow.Repository<SalaryStructure>().GetByIdAsync(request.SalaryStructureId, ct);
        if (structure is null)
            return Result.Failure("Struktur gaji tidak ditemukan.");

        var component = await _uow.Repository<SalaryComponent>().GetByIdAsync(request.SalaryComponentId, ct);
        if (component is null)
            return Result.Failure("Komponen gaji tidak ditemukan.");

        var alreadyAdded = await _uow.Repository<SalaryStructureComponent>().FindAsync(
            sc => sc.SalaryStructureId == request.SalaryStructureId && sc.SalaryComponentId == request.SalaryComponentId, ct);
        if (alreadyAdded.Count > 0)
            return Result.Failure("Komponen ini sudah ada di struktur gaji tersebut.");

        var existingCount = (await _uow.Repository<SalaryStructureComponent>().FindAsync(
            sc => sc.SalaryStructureId == request.SalaryStructureId, ct)).Count;

        await _uow.Repository<SalaryStructureComponent>().AddAsync(new SalaryStructureComponent
        {
            SalaryStructureId = request.SalaryStructureId,
            SalaryComponentId = request.SalaryComponentId,
            AmountOverride = request.AmountOverride,
            PercentageOverride = request.PercentageOverride,
            SortOrder = existingCount
        }, ct);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<List<SalaryStructureAssignmentDto>> GetAssignmentsAsync(CancellationToken ct = default)
    {
        var assignments = await _uow.Repository<SalaryStructureAssignment>().GetAllAsync(ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var structures = await _uow.Repository<SalaryStructure>().GetAllAsync(ct);

        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);
        var structureNames = structures.ToDictionary(s => s.Id, s => s.Name);

        return assignments
            .OrderByDescending(a => a.FromDate)
            .Select(a => new SalaryStructureAssignmentDto(
                a.Id, a.EmployeeId, employeeNames.GetValueOrDefault(a.EmployeeId),
                a.SalaryStructureId, structureNames.GetValueOrDefault(a.SalaryStructureId),
                a.BaseSalary, a.FromDate))
            .ToList();
    }

    public async Task<Result<SalaryStructureAssignmentDto>> AssignAsync(AssignSalaryStructureRequest request, CancellationToken ct = default)
    {
        if (request.BaseSalary <= 0)
            return Result<SalaryStructureAssignmentDto>.Failure("Gaji pokok harus lebih dari 0.");

        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct);
        if (employee is null)
            return Result<SalaryStructureAssignmentDto>.Failure("Pegawai tidak ditemukan.");

        var structure = await _uow.Repository<SalaryStructure>().GetByIdAsync(request.SalaryStructureId, ct);
        if (structure is null)
            return Result<SalaryStructureAssignmentDto>.Failure("Struktur gaji tidak ditemukan.");

        // BUG FIX: without this check, an employee from Company A could be assigned a
        // SalaryStructure that belongs to Company B (both dropdowns are populated from the
        // whole tenant, not cross-filtered) — silently mixing payroll data across companies.
        if (structure.CompanyId != employee.CompanyId)
            return Result<SalaryStructureAssignmentDto>.Failure("Struktur gaji ini milik perusahaan yang berbeda dari perusahaan pegawai.");

        var assignment = new SalaryStructureAssignment
        {
            EmployeeId = request.EmployeeId,
            SalaryStructureId = request.SalaryStructureId,
            BaseSalary = request.BaseSalary,
            FromDate = request.FromDate
        };

        await _uow.Repository<SalaryStructureAssignment>().AddAsync(assignment, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<SalaryStructureAssignmentDto>.Success(new SalaryStructureAssignmentDto(
            assignment.Id, assignment.EmployeeId, employee.FullName,
            assignment.SalaryStructureId, structure.Name, assignment.BaseSalary, assignment.FromDate));
    }

    private async Task<SalaryStructureDto> MapToDtoAsync(SalaryStructure structure, string? companyName, CancellationToken ct)
    {
        var structureComponents = await _uow.Repository<SalaryStructureComponent>().FindAsync(
            sc => sc.SalaryStructureId == structure.Id, ct);
        var components = await _uow.Repository<SalaryComponent>().GetAllAsync(ct);
        var componentLookup = components.ToDictionary(c => c.Id);

        var componentDtos = structureComponents
            .OrderBy(sc => sc.SortOrder)
            .Select(sc =>
            {
                componentLookup.TryGetValue(sc.SalaryComponentId, out var comp);
                return new SalaryStructureComponentDto(
                    sc.Id, sc.SalaryComponentId, comp?.Name, comp?.ComponentType ?? default,
                    comp?.CalculationType ?? default, sc.AmountOverride, sc.PercentageOverride);
            })
            .ToList();

        return new SalaryStructureDto(structure.Id, structure.Name, structure.CompanyId, companyName, structure.IsActive, componentDtos);
    }
}
