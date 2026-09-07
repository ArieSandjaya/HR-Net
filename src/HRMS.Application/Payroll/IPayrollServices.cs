using HRMS.Application.Common.Models;

namespace HRMS.Application.Payroll;

public interface ISalaryComponentService
{
    Task<List<SalaryComponentDto>> GetAllAsync(CancellationToken ct = default);
    Task<Result<SalaryComponentDto>> CreateAsync(CreateSalaryComponentRequest request, CancellationToken ct = default);
}

public interface ISalaryStructureService
{
    Task<List<SalaryStructureDto>> GetAllAsync(CancellationToken ct = default);
    Task<SalaryStructureDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<SalaryStructureDto>> CreateAsync(CreateSalaryStructureRequest request, CancellationToken ct = default);
    Task<Result> AddComponentAsync(AddStructureComponentRequest request, CancellationToken ct = default);

    Task<List<SalaryStructureAssignmentDto>> GetAssignmentsAsync(CancellationToken ct = default);
    Task<Result<SalaryStructureAssignmentDto>> AssignAsync(AssignSalaryStructureRequest request, CancellationToken ct = default);
}

public interface IPayrollEntryService
{
    Task<List<PayrollEntryDto>> GetAllAsync(CancellationToken ct = default);
    Task<List<SalarySlipDto>> GetSalarySlipsAsync(Guid payrollEntryId, CancellationToken ct = default);

    /// <summary>Creates the PayrollEntry and immediately generates a SalarySlip for every
    /// employee with an active SalaryStructureAssignment as of PeriodStart — prorating
    /// BaseSalary against Absent days recorded in the Attendance module for that period.</summary>
    Task<Result<PayrollEntryDto>> RunPayrollAsync(CreatePayrollEntryRequest request, CancellationToken ct = default);
}
