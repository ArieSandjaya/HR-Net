using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Entities.Tenancy;
using HRMS.Domain.Enums;

namespace HRMS.Application.Employees;

public class EmployeeService : IEmployeeService
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenantProvider;

    public EmployeeService(IUnitOfWork uow, ICurrentUserService currentUser, ITenantProvider tenantProvider)
    {
        _uow = uow;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
    }

    public async Task<List<EmployeeDto>> GetAllAsync(CancellationToken ct = default)
    {
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        return employees.Select(MapToDto).ToList();
    }

    public async Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var employee = await _uow.Repository<Employee>().GetByIdAsync(id, ct);
        return employee is null ? null : MapToDto(employee);
    }

    public async Task<Result<EmployeeDto>> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Employee>();

        var emailTaken = await repo.FirstOrDefaultAsync(e => e.Email == request.Email, ct);
        if (emailTaken is not null)
            return Result<EmployeeDto>.Failure($"Email '{request.Email}' sudah terdaftar pada pegawai lain.");

        if (request.DateOfJoining < request.DateOfBirth.AddYears(15))
            return Result<EmployeeDto>.Failure("Tanggal bergabung tidak valid relatif terhadap tanggal lahir.");

        // BUG FIX: Department and Branch both carry their own CompanyId (a tenant can have
        // multiple companies), but nothing previously checked that the selected Department/
        // Branch actually belongs to the Company being assigned — the dropdowns in the UI
        // aren't cross-filtered, so it was possible to silently create an employee whose
        // Company and Department point at two different companies.
        var department = await _uow.Repository<Department>().GetByIdAsync(request.DepartmentId, ct);
        if (department is null)
            return Result<EmployeeDto>.Failure("Departemen tidak ditemukan.");
        if (department.CompanyId != request.CompanyId)
            return Result<EmployeeDto>.Failure("Departemen yang dipilih bukan milik perusahaan ini.");

        if (request.BranchId.HasValue)
        {
            var branch = await _uow.Repository<Branch>().GetByIdAsync(request.BranchId.Value, ct);
            if (branch is null)
                return Result<EmployeeDto>.Failure("Cabang tidak ditemukan.");
            if (branch.CompanyId != request.CompanyId)
                return Result<EmployeeDto>.Failure("Cabang yang dipilih bukan milik perusahaan ini.");
        }

        var employeeNumber = await GenerateNextEmployeeNumberAsync(ct);

        var employee = new Employee
        {
            EmployeeNumber = employeeNumber,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Email = request.Email,
            MobileNumber = request.MobileNumber,
            DateOfJoining = request.DateOfJoining,
            Status = EmployeeStatus.Active,
            CompanyId = request.CompanyId,
            DepartmentId = request.DepartmentId,
            DesignationId = request.DesignationId,
            EmploymentTypeId = request.EmploymentTypeId,
            ReportsToEmployeeId = request.ReportsToEmployeeId,
            BranchId = request.BranchId,
            HolidayListId = request.HolidayListId,
            CreatedBy = _currentUser.UserName
        };

        await repo.AddAsync(employee, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<EmployeeDto>.Success(MapToDto(employee));
    }

    public async Task<Result<EmployeeDto>> UpdateAsync(UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Employee>();
        var employee = await repo.GetByIdAsync(request.Id, ct);
        if (employee is null)
            return Result<EmployeeDto>.Failure("Pegawai tidak ditemukan.");

        if (request.ReportsToEmployeeId == request.Id)
            return Result<EmployeeDto>.Failure("Pegawai tidak boleh melapor kepada dirinya sendiri.");

        var department = await _uow.Repository<Department>().GetByIdAsync(request.DepartmentId, ct);
        if (department is null)
            return Result<EmployeeDto>.Failure("Departemen tidak ditemukan.");
        if (department.CompanyId != employee.CompanyId)
            return Result<EmployeeDto>.Failure("Departemen yang dipilih bukan milik perusahaan pegawai ini.");

        employee.FirstName = request.FirstName;
        employee.MiddleName = request.MiddleName;
        employee.LastName = request.LastName;
        employee.Email = request.Email;
        employee.MobileNumber = request.MobileNumber;
        employee.DepartmentId = request.DepartmentId;
        employee.DesignationId = request.DesignationId;
        employee.ReportsToEmployeeId = request.ReportsToEmployeeId;
        employee.Status = request.Status;
        employee.ModifiedAt = DateTime.UtcNow;
        employee.ModifiedBy = _currentUser.UserName;

        repo.Update(employee);
        await _uow.SaveChangesAsync(ct);

        return Result<EmployeeDto>.Success(MapToDto(employee));
    }

    public async Task<Result> MarkAsRelievedAsync(Guid employeeId, DateOnly relievingDate, CancellationToken ct = default)
    {
        var repo = _uow.Repository<Employee>();
        var employee = await repo.GetByIdAsync(employeeId, ct);
        if (employee is null)
            return Result.Failure("Pegawai tidak ditemukan.");

        if (relievingDate < employee.DateOfJoining)
            return Result.Failure("Tanggal relieving tidak boleh sebelum tanggal bergabung.");

        employee.RelievingDate = relievingDate;
        employee.Status = EmployeeStatus.LeftOrganization;
        repo.Update(employee);
        await _uow.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// BUG FIX: previously this counted current (non-deleted) Employee rows and used
    /// count+1 as the next number. Because Employees are soft-deleted rather than removed,
    /// that approach could regenerate an EmployeeNumber that still exists (hidden) in the
    /// database, colliding with the unique (TenantId, EmployeeNumber) index the next time an
    /// employee was created after a deletion. A persistent per-tenant counter avoids that.
    /// </summary>
    private async Task<string> GenerateNextEmployeeNumberAsync(CancellationToken ct)
    {
        var tenantRepo = _uow.Repository<Tenant>();
        var tenant = await tenantRepo.GetByIdAsync(_tenantProvider.TenantId, ct);
        if (tenant is null)
            throw new InvalidOperationException("Tidak bisa membuat pegawai: konteks tenant saat ini tidak valid.");

        tenant.EmployeeSequence += 1;
        tenantRepo.Update(tenant);

        return $"EMP-{tenant.EmployeeSequence:D5}";
    }

    private static EmployeeDto MapToDto(Employee e) => new(
        e.Id,
        e.EmployeeNumber,
        e.FullName,
        e.Email,
        e.MobileNumber,
        e.DateOfJoining,
        e.Status,
        e.DepartmentId,
        e.Department?.Name,
        e.DesignationId,
        e.Designation?.Name,
        e.ReportsToEmployeeId,
        e.ReportsTo?.FullName
    );
}
