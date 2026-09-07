using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Expense;
using HRMS.Domain.Enums;

namespace HRMS.Application.Expense;

public class ExpenseClaimService : IExpenseClaimService
{
    private readonly IUnitOfWork _uow;

    public ExpenseClaimService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<ExpenseClaimDto>> GetAllAsync(CancellationToken ct = default)
    {
        var claims = await _uow.Repository<ExpenseClaim>().GetAllAsync(ct);
        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);
        var categories = await _uow.Repository<ExpenseCategory>().GetAllAsync(ct);
        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);
        var allDetails = await _uow.Repository<ExpenseClaimDetail>().GetAllAsync(ct);

        return claims
            .OrderByDescending(c => c.ClaimDate)
            .Select(c => MapToDto(c, employeeNames, categoryNames, allDetails.Where(d => d.ExpenseClaimId == c.Id).ToList()))
            .ToList();
    }

    public async Task<Result<ExpenseClaimDto>> CreateAsync(CreateExpenseClaimRequest request, CancellationToken ct = default)
    {
        if (request.Lines.Count == 0)
            return Result<ExpenseClaimDto>.Failure("Klaim harus punya minimal satu item pengeluaran.");
        if (request.Lines.Any(l => l.Amount <= 0))
            return Result<ExpenseClaimDto>.Failure("Semua item pengeluaran harus punya nominal lebih dari 0.");

        var employee = await _uow.Repository<Employee>().GetByIdAsync(request.EmployeeId, ct);
        if (employee is null)
            return Result<ExpenseClaimDto>.Failure("Pegawai tidak ditemukan.");

        var categoryIds = request.Lines.Select(l => l.ExpenseCategoryId).Distinct().ToList();
        var categories = await _uow.Repository<ExpenseCategory>().FindAsync(c => categoryIds.Contains(c.Id), ct);
        if (categories.Count != categoryIds.Count)
            return Result<ExpenseClaimDto>.Failure("Ada kategori pengeluaran yang tidak ditemukan.");

        var claim = new ExpenseClaim
        {
            EmployeeId = request.EmployeeId,
            ClaimDate = DateOnly.FromDateTime(DateTime.Today),
            TotalAmount = request.Lines.Sum(l => l.Amount),
            Status = ExpenseClaimStatus.Pending,
            DocStatus = DocumentStatus.Submitted
        };
        await _uow.Repository<ExpenseClaim>().AddAsync(claim, ct);

        var details = request.Lines.Select(l => new ExpenseClaimDetail
        {
            ExpenseClaimId = claim.Id,
            ExpenseCategoryId = l.ExpenseCategoryId,
            ExpenseDate = l.ExpenseDate,
            Description = l.Description,
            Amount = l.Amount
        }).ToList();

        foreach (var d in details)
            await _uow.Repository<ExpenseClaimDetail>().AddAsync(d, ct);

        await _uow.SaveChangesAsync(ct);

        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);
        var employeeNames = new Dictionary<Guid, string> { [employee.Id] = employee.FullName };

        return Result<ExpenseClaimDto>.Success(MapToDto(claim, employeeNames, categoryNames, details));
    }

    public async Task<Result> ApproveAsync(Guid expenseClaimId, Guid approverEmployeeId, CancellationToken ct = default)
    {
        var repo = _uow.Repository<ExpenseClaim>();
        var claim = await repo.GetByIdAsync(expenseClaimId, ct);
        if (claim is null)
            return Result.Failure("Klaim tidak ditemukan.");
        if (claim.Status != ExpenseClaimStatus.Pending)
            return Result.Failure("Hanya klaim berstatus Pending yang bisa disetujui.");

        claim.Status = ExpenseClaimStatus.Approved;
        claim.ApprovedByEmployeeId = approverEmployeeId;
        claim.ApprovedAt = DateTime.UtcNow;
        repo.Update(claim);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid expenseClaimId, Guid approverEmployeeId, string reason, CancellationToken ct = default)
    {
        var repo = _uow.Repository<ExpenseClaim>();
        var claim = await repo.GetByIdAsync(expenseClaimId, ct);
        if (claim is null)
            return Result.Failure("Klaim tidak ditemukan.");
        if (claim.Status != ExpenseClaimStatus.Pending)
            return Result.Failure("Hanya klaim berstatus Pending yang bisa ditolak.");

        claim.Status = ExpenseClaimStatus.Rejected;
        claim.ApprovedByEmployeeId = approverEmployeeId;
        claim.RejectionReason = reason;
        repo.Update(claim);

        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static ExpenseClaimDto MapToDto(
        ExpenseClaim c, Dictionary<Guid, string> employeeNames, Dictionary<Guid, string> categoryNames, List<ExpenseClaimDetail> details) => new(
        c.Id, c.EmployeeId, employeeNames.GetValueOrDefault(c.EmployeeId), c.ClaimDate, c.TotalAmount,
        c.Status, c.ApprovedByEmployeeId, c.ApprovedByEmployeeId.HasValue ? employeeNames.GetValueOrDefault(c.ApprovedByEmployeeId.Value) : null,
        c.RejectionReason,
        details.Select(d => new ExpenseClaimDetailDto(d.Id, d.ExpenseCategoryId, categoryNames.GetValueOrDefault(d.ExpenseCategoryId), d.ExpenseDate, d.Description, d.Amount)).ToList());
}
