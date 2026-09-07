using HRMS.Application.Common.Models;

namespace HRMS.Application.Expense;

public interface IExpenseCategoryService
{
    Task<List<ExpenseCategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ExpenseCategoryDto>> CreateAsync(CreateExpenseCategoryRequest request, CancellationToken ct = default);
}

public interface IExpenseClaimService
{
    Task<List<ExpenseClaimDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<ExpenseClaimDto>> CreateAsync(CreateExpenseClaimRequest request, CancellationToken ct = default);
    Task<Result> ApproveAsync(Guid expenseClaimId, Guid approverEmployeeId, CancellationToken ct = default);
    Task<Result> RejectAsync(Guid expenseClaimId, Guid approverEmployeeId, string reason, CancellationToken ct = default);
}

public interface IEmployeeAdvanceService
{
    Task<List<EmployeeAdvanceDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<EmployeeAdvanceDto>> CreateAsync(CreateEmployeeAdvanceRequest request, CancellationToken ct = default);
    Task<Result> ApproveAsync(Guid advanceId, Guid approverEmployeeId, CancellationToken ct = default);
    Task<Result> RejectAsync(Guid advanceId, Guid approverEmployeeId, CancellationToken ct = default);
    Task<Result> MarkPaidAsync(Guid advanceId, CancellationToken ct = default);
}

public interface ITravelRequestService
{
    Task<List<TravelRequestDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<TravelRequestDto>> CreateAsync(CreateTravelRequestRequest request, CancellationToken ct = default);
    Task<Result> ApproveAsync(Guid travelRequestId, Guid approverEmployeeId, CancellationToken ct = default);
    Task<Result> RejectAsync(Guid travelRequestId, Guid approverEmployeeId, CancellationToken ct = default);
}
