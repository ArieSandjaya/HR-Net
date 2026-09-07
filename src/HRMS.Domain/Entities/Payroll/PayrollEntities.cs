using HRMS.Domain.Common;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Payroll;

/// <summary>A single line item that can appear on a payslip (e.g. "Gaji Pokok", "Tunjangan
/// Transport", "BPJS Kesehatan", "PPh21"). Deliberately simple: fixed amount or percentage
/// of the employee's base salary — no formula engine (see README for rationale).</summary>
public class SalaryComponent : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public SalaryComponentType ComponentType { get; set; }
    public SalaryCalculationType CalculationType { get; set; } = SalaryCalculationType.FixedAmount;

    /// <summary>Used when CalculationType == FixedAmount (default amount if the structure
    /// doesn't override it).</summary>
    public decimal? DefaultAmount { get; set; }

    /// <summary>Used when CalculationType == PercentageOfBasic (0-100).</summary>
    public decimal? DefaultPercentage { get; set; }

    /// <summary>Whether this earning counts toward taxable income — reserved for future use
    /// once a proper tax engine is built; not yet used in calculation.</summary>
    public bool IsTaxable { get; set; } = true;
}

/// <summary>A reusable template of components, assigned to employees via
/// SalaryStructureAssignment. Mirrors Frappe's "Salary Structure".</summary>
public class SalaryStructure : TenantEntity
{
    public string Name { get; set; } = string.Empty;

    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<SalaryStructureComponent> Components { get; set; } = new List<SalaryStructureComponent>();
}

/// <summary>Junction: which SalaryComponents belong to a SalaryStructure, with optional
/// overrides of the component's default amount/percentage.</summary>
public class SalaryStructureComponent : TenantEntity
{
    public Guid SalaryStructureId { get; set; }
    public SalaryStructure? SalaryStructure { get; set; }

    public Guid SalaryComponentId { get; set; }
    public SalaryComponent? SalaryComponent { get; set; }

    public decimal? AmountOverride { get; set; }
    public decimal? PercentageOverride { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Assigns a SalaryStructure to an Employee from a given date, with that employee's
/// base salary ("Gaji Pokok") — the figure percentage-based components calculate off of.</summary>
public class SalaryStructureAssignment : TenantEntity
{
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public Guid SalaryStructureId { get; set; }
    public SalaryStructure? SalaryStructure { get; set; }

    public decimal BaseSalary { get; set; }
    public DateOnly FromDate { get; set; }
}

/// <summary>One payroll "run" for a Company covering one period — generates a SalarySlip for
/// every employee with an active SalaryStructureAssignment as of that period.</summary>
public class PayrollEntry : SubmittableEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public ICollection<SalarySlip> SalarySlips { get; set; } = new List<SalarySlip>();
}

public class SalarySlip : SubmittableEntity
{
    public Guid PayrollEntryId { get; set; }
    public PayrollEntry? PayrollEntry { get; set; }

    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }

    public decimal BaseSalary { get; set; }

    /// <summary>Days counted as Absent in the period (from AttendanceRecord) that prorate
    /// BaseSalary downward — see PayrollEntryService for the calculation.</summary>
    public int UnpaidAbsentDays { get; set; }
    public int WorkingDaysInPeriod { get; set; }

    public decimal GrossPay { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPay { get; set; }

    public ICollection<SalarySlipComponent> Components { get; set; } = new List<SalarySlipComponent>();
}

/// <summary>A computed line item on a payslip. Component name/type are denormalized
/// (copied at generation time) so historical payslips stay accurate even if the
/// SalaryComponent definition changes or is later removed from the structure.</summary>
public class SalarySlipComponent : TenantEntity
{
    public Guid SalarySlipId { get; set; }
    public SalarySlip? SalarySlip { get; set; }

    public Guid SalaryComponentId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public SalaryComponentType ComponentType { get; set; }
    public decimal Amount { get; set; }
}
