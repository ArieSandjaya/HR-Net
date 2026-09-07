using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Enums;

namespace HRMS.Application.Payroll;

public class SalaryComponentService : ISalaryComponentService
{
    private readonly IUnitOfWork _uow;

    public SalaryComponentService(IUnitOfWork uow) => _uow = uow;

    public async Task<List<SalaryComponentDto>> GetAllAsync(CancellationToken ct = default)
    {
        var components = await _uow.Repository<SalaryComponent>().GetAllAsync(ct);
        return components.Select(MapToDto).ToList();
    }

    public async Task<Result<SalaryComponentDto>> CreateAsync(CreateSalaryComponentRequest request, CancellationToken ct = default)
    {
        if (request.CalculationType == SalaryCalculationType.FixedAmount && !request.DefaultAmount.HasValue)
            return Result<SalaryComponentDto>.Failure("Nominal wajib diisi untuk komponen bertipe jumlah tetap.");
        if (request.CalculationType == SalaryCalculationType.PercentageOfBasic &&
            (!request.DefaultPercentage.HasValue || request.DefaultPercentage is < 0 or > 100))
            return Result<SalaryComponentDto>.Failure("Persentase harus diisi antara 0-100 untuk komponen bertipe persentase.");

        var component = new SalaryComponent
        {
            Name = request.Name,
            ComponentType = request.ComponentType,
            CalculationType = request.CalculationType,
            DefaultAmount = request.DefaultAmount,
            DefaultPercentage = request.DefaultPercentage,
            IsTaxable = request.IsTaxable
        };

        await _uow.Repository<SalaryComponent>().AddAsync(component, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<SalaryComponentDto>.Success(MapToDto(component));
    }

    private static SalaryComponentDto MapToDto(SalaryComponent c) => new(
        c.Id, c.Name, c.ComponentType, c.CalculationType, c.DefaultAmount, c.DefaultPercentage, c.IsTaxable);
}
