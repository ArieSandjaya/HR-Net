using HRMS.Application.Common.Models;

namespace HRMS.Application.Employees;

public interface IEmployeeService
{
    Task<List<EmployeeDto>> GetAllAsync(CancellationToken ct = default);
    Task<EmployeeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<EmployeeDto>> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default);
    Task<Result<EmployeeDto>> UpdateAsync(UpdateEmployeeRequest request, CancellationToken ct = default);
    Task<Result> MarkAsRelievedAsync(Guid employeeId, DateOnly relievingDate, CancellationToken ct = default);
}
