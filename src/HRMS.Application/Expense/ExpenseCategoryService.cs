using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Expense;

namespace HRMS.Application.Expense;

public class ExpenseCategoryService : IExpenseCategoryService
{
    private readonly IUnitOfWork _uow;

    public ExpenseCategoryService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<ExpenseCategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _uow.Repository<ExpenseCategory>().GetAllAsync(ct);
        return categories.Select(c => new ExpenseCategoryDto(c.Id, c.Name)).ToList();
    }

    public async Task<Result<ExpenseCategoryDto>> CreateAsync(CreateExpenseCategoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<ExpenseCategoryDto>.Failure("Nama kategori tidak boleh kosong.");

        var category = new ExpenseCategory { Name = request.Name };
        await _uow.Repository<ExpenseCategory>().AddAsync(category, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<ExpenseCategoryDto>.Success(new ExpenseCategoryDto(category.Id, category.Name));
    }
}
