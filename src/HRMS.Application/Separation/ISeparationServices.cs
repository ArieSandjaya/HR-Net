using HRMS.Application.Common.Models;

namespace HRMS.Application.Separation;

public interface IEmployeeSeparationService
{
    Task<List<EmployeeSeparationDto>> GetAllAsync(CancellationToken ct = default);
    Task<EmployeeSeparationDto?> GetByIdAsync(Guid separationId, CancellationToken ct = default);
    Task<Result<EmployeeSeparationDto>> CreateAsync(CreateEmployeeSeparationRequest request, CancellationToken ct = default);
    Task<Result> ApproveAsync(ApproveSeparationRequest request, CancellationToken ct = default);

    /// <summary>Finalizes the separation: flips the Employee record to LeftOrganization via
    /// EmployeeService.MarkAsRelievedAsync. Only allowed once Status is Approved.</summary>
    Task<Result> CompleteAsync(Guid separationId, CancellationToken ct = default);
}

public interface IExitInterviewService
{
    Task<List<ExitInterviewDto>> GetBySeparationAsync(Guid separationId, CancellationToken ct = default);
    Task<Result<ExitInterviewDto>> RecordAsync(RecordExitInterviewRequest request, CancellationToken ct = default);
}

public interface IFnFSettlementService
{
    Task<FullAndFinalSettlementDto?> GetBySeparationAsync(Guid separationId, CancellationToken ct = default);

    /// <summary>Creates the settlement draft for a separation and auto-populates it with:
    /// (a) a leave encashment credit computed from the employee's current balance across
    /// LeaveTypes marked IsEncashable, priced at their latest SalaryStructureAssignment's
    /// per-day rate, and (b) a deduction for any EmployeeAdvance still in "Paid" status
    /// (approved and disbursed, never settled against an expense claim).</summary>
    Task<Result<FullAndFinalSettlementDto>> GenerateAsync(Guid separationId, CancellationToken ct = default);

    Task<Result> AddComponentAsync(AddFnFComponentRequest request, CancellationToken ct = default);
    Task<Result> FinalizeAsync(Guid settlementId, CancellationToken ct = default);
}
