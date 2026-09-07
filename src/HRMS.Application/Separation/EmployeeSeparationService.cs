using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Application.Employees;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Separation;
using HRMS.Domain.Enums;

namespace HRMS.Application.Separation;

public class EmployeeSeparationService : IEmployeeSeparationService
{
    private readonly IUnitOfWork _uow;
    private readonly IEmployeeService _employeeService;

    public EmployeeSeparationService(IUnitOfWork uow, IEmployeeService employeeService)
    {
        _uow = uow;
        _employeeService = employeeService;
    }

    public async Task<List<EmployeeSeparationDto>> GetAllAsync(CancellationToken ct = default)
    {
        var separations = await _uow.Repository<EmployeeSeparation>().GetAllAsync(ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);

        return separations
            .OrderByDescending(s => s.ResignationDate)
            .Select(s => MapToDto(s, employeeNames))
            .ToList();
    }

    public async Task<EmployeeSeparationDto?> GetByIdAsync(Guid separationId, CancellationToken ct = default)
    {
        var separation = await _uow.Repository<EmployeeSeparation>().GetByIdAsync(separationId, ct);
        if (separation is null) return null;

        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        return MapToDto(separation, employees.ToDictionary(e => e.Id, e => e.FullName));
    }

    public async Task<Result<EmployeeSeparationDto>> CreateAsync(CreateEmployeeSeparationRequest request, CancellationToken ct = default)
    {
        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct);
        if (employee is null)
            return Result<EmployeeSeparationDto>.Failure("Pegawai tidak ditemukan.");
        if (employee.Status != EmployeeStatus.Active)
            return Result<EmployeeSeparationDto>.Failure("Pegawai ini sudah tidak berstatus aktif.");

        var existing = await _uow.Repository<EmployeeSeparation>().FindAsync(
            s => s.EmployeeId == request.EmployeeId && s.Status != SeparationStatus.Completed, ct);
        if (existing.Count > 0)
            return Result<EmployeeSeparationDto>.Failure("Pegawai ini sudah punya proses separation yang sedang berjalan.");

        var separation = new EmployeeSeparation
        {
            EmployeeId = request.EmployeeId,
            ResignationDate = request.ResignationDate,
            Reason = request.Reason,
            SeparationType = request.SeparationType,
            Status = SeparationStatus.Pending,
            DocStatus = DocumentStatus.Draft
        };

        await _uow.Repository<EmployeeSeparation>().AddAsync(separation, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<EmployeeSeparationDto>.Success(MapToDto(separation, new Dictionary<Guid, string> { [employee.Id] = employee.FullName }));
    }

    public async Task<Result> ApproveAsync(ApproveSeparationRequest request, CancellationToken ct = default)
    {
        var repo = _uow.Repository<EmployeeSeparation>();
        var separation = await repo.GetByIdAsync(request.SeparationId, ct);
        if (separation is null)
            return Result.Failure("Data separation tidak ditemukan.");
        if (separation.Status != SeparationStatus.Pending)
            return Result.Failure("Hanya separation berstatus Pending yang bisa disetujui.");
        if (request.RelievingDate < separation.ResignationDate)
            return Result.Failure("Tanggal relieving tidak boleh sebelum tanggal pengajuan resign.");

        separation.Status = SeparationStatus.Approved;
        separation.RelievingDate = request.RelievingDate;
        separation.ApprovedByEmployeeId = request.ApproverEmployeeId;
        separation.DocStatus = DocumentStatus.Submitted;

        repo.Update(separation);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> CompleteAsync(Guid separationId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<EmployeeSeparation>();
        var separation = await repo.GetByIdAsync(separationId, ct);
        if (separation is null)
            return Result.Failure("Data separation tidak ditemukan.");
        if (separation.Status != SeparationStatus.Approved)
            return Result.Failure("Separation harus disetujui (Approved) dulu sebelum bisa diselesaikan.");
        if (!separation.RelievingDate.HasValue)
            return Result.Failure("Tanggal relieving belum ditetapkan.");

        // The actual integration point: this is what flips Employee.Status to LeftOrganization —
        // reuses EmployeeService.MarkAsRelievedAsync, which has existed since Phase 1 but was
        // never called from anywhere until this workflow was built.
        var relieveResult = await _employeeService.MarkAsRelievedAsync(separation.EmployeeId, separation.RelievingDate.Value, ct);
        if (!relieveResult.Succeeded)
            return relieveResult;

        separation.Status = SeparationStatus.Completed;
        repo.Update(separation);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static EmployeeSeparationDto MapToDto(EmployeeSeparation s, Dictionary<Guid, string> employeeNames) => new(
        s.Id, s.EmployeeId, employeeNames.GetValueOrDefault(s.EmployeeId), s.ResignationDate, s.RelievingDate,
        s.Reason, s.SeparationType, s.Status,
        s.ApprovedByEmployeeId, s.ApprovedByEmployeeId.HasValue ? employeeNames.GetValueOrDefault(s.ApprovedByEmployeeId.Value) : null);
}
