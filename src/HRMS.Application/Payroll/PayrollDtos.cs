using HRMS.Domain.Enums;

namespace HRMS.Application.Payroll;

// ---- Salary Component ----
public record SalaryComponentDto(
    Guid Id, string Name, SalaryComponentType ComponentType, SalaryCalculationType CalculationType,
    decimal? DefaultAmount, decimal? DefaultPercentage, bool IsTaxable
);

public record CreateSalaryComponentRequest(
    string Name, SalaryComponentType ComponentType, SalaryCalculationType CalculationType,
    decimal? DefaultAmount, decimal? DefaultPercentage, bool IsTaxable
);

// ---- Salary Structure ----
public record SalaryStructureDto(
    Guid Id, string Name, Guid CompanyId, string? CompanyName, bool IsActive,
    List<SalaryStructureComponentDto> Components
);

public record SalaryStructureComponentDto(
    Guid Id, Guid SalaryComponentId, string? ComponentName, SalaryComponentType ComponentType,
    SalaryCalculationType CalculationType, decimal? AmountOverride, decimal? PercentageOverride
);

public record CreateSalaryStructureRequest(string Name, Guid CompanyId);

public record AddStructureComponentRequest(
    Guid SalaryStructureId, Guid SalaryComponentId, decimal? AmountOverride, decimal? PercentageOverride
);

// ---- Salary Structure Assignment ----
public record SalaryStructureAssignmentDto(
    Guid Id, Guid EmployeeId, string? EmployeeName, Guid SalaryStructureId, string? SalaryStructureName,
    decimal BaseSalary, DateOnly FromDate
);

public record AssignSalaryStructureRequest(Guid EmployeeId, Guid SalaryStructureId, decimal BaseSalary, DateOnly FromDate);

// ---- Payroll Entry & Salary Slip ----
public record CreatePayrollEntryRequest(Guid CompanyId, DateOnly PeriodStart, DateOnly PeriodEnd);

public record PayrollEntryDto(
    Guid Id, Guid CompanyId, string? CompanyName, DateOnly PeriodStart, DateOnly PeriodEnd,
    Domain.Common.DocumentStatus DocStatus, int SalarySlipCount, decimal TotalNetPay
);

public record SalarySlipComponentDto(string ComponentName, SalaryComponentType ComponentType, decimal Amount);

public record SalarySlipDto(
    Guid Id, Guid EmployeeId, string? EmployeeName, string? EmployeeNumber,
    DateOnly PeriodStart, DateOnly PeriodEnd, decimal BaseSalary,
    int UnpaidAbsentDays, int WorkingDaysInPeriod,
    decimal GrossPay, decimal TotalDeductions, decimal NetPay,
    List<SalarySlipComponentDto> Components
);
