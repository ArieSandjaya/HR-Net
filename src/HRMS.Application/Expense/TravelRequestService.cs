using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Expense;
using HRMS.Domain.Enums;

namespace HRMS.Application.Expense;

public class TravelRequestService : ITravelRequestService
{
    private readonly IUnitOfWork _uow;

    public TravelRequestService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<TravelRequestDto>> GetAllAsync(CancellationToken ct = default)
    {
        var requests = await _uow.Repository<TravelRequest>().GetAllAsync(ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);

        return requests
            .OrderByDescending(r => r.FromDate)
            .Select(r => new TravelRequestDto(
                r.Id, r.EmployeeId, employeeNames.GetValueOrDefault(r.EmployeeId), r.Destination, r.Purpose,
                r.FromDate, r.ToDate, r.EstimatedCost, r.Status,
                r.ApprovedByEmployeeId, r.ApprovedByEmployeeId.HasValue ? employeeNames.GetValueOrDefault(r.ApprovedByEmployeeId.Value) : null))
            .ToList();
    }

    public async Task<Result<TravelRequestDto>> CreateAsync(CreateTravelRequestRequest request, CancellationToken ct = default)
    {
        if (request.ToDate < request.FromDate)
            return Result<TravelRequestDto>.Failure("Tanggal pulang tidak boleh sebelum tanggal berangkat.");
        if (string.IsNullOrWhiteSpace(request.Destination))
            return Result<TravelRequestDto>.Failure("Tujuan perjalanan tidak boleh kosong.");

        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct);
        if (employee is null)
            return Result<TravelRequestDto>.Failure("Pegawai tidak ditemukan.");

        var travelRequest = new TravelRequest
        {
            EmployeeId = request.EmployeeId,
            Destination = request.Destination,
            Purpose = request.Purpose,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            EstimatedCost = request.EstimatedCost,
            Status = TravelRequestStatus.Pending,
            DocStatus = DocumentStatus.Submitted
        };

        await _uow.Repository<TravelRequest>().AddAsync(travelRequest, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<TravelRequestDto>.Success(new TravelRequestDto(
            travelRequest.Id, travelRequest.EmployeeId, employee.FullName, travelRequest.Destination, travelRequest.Purpose,
            travelRequest.FromDate, travelRequest.ToDate, travelRequest.EstimatedCost, travelRequest.Status, null, null));
    }

    public async Task<Result> ApproveAsync(Guid travelRequestId, Guid approverEmployeeId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<TravelRequest>();
        var request = await repo.GetByIdAsync(travelRequestId, ct);
        if (request is null)
            return Result.Failure("Pengajuan perjalanan tidak ditemukan.");
        if (request.Status != TravelRequestStatus.Pending)
            return Result.Failure("Hanya pengajuan berstatus Pending yang bisa disetujui.");

        request.Status = TravelRequestStatus.Approved;
        request.ApprovedByEmployeeId = approverEmployeeId;
        repo.Update(request);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid travelRequestId, Guid approverEmployeeId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<TravelRequest>();
        var request = await repo.GetByIdAsync(travelRequestId, ct);
        if (request is null)
            return Result.Failure("Pengajuan perjalanan tidak ditemukan.");
        if (request.Status != TravelRequestStatus.Pending)
            return Result.Failure("Hanya pengajuan berstatus Pending yang bisa ditolak.");

        request.Status = TravelRequestStatus.Rejected;
        request.ApprovedByEmployeeId = approverEmployeeId;
        repo.Update(request);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
