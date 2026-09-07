using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Organization;

/// <summary>Equivalent of ERPNext "Company" — top of the org hierarchy.</summary>
public class Company : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string? DefaultCurrency { get; set; } = "IDR";
    public string? Address { get; set; }
    public string? TaxId { get; set; }

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<Department> Departments { get; set; } = new List<Department>();
}

public class Branch : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
}

public class Department : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    public Guid? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }

    public Guid? DepartmentHeadEmployeeId { get; set; }
}

public class Designation : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class EmploymentType : TenantEntity
{
    /// <summary>e.g. Full-time, Part-time, Contract, Intern, Probation</summary>
    public string Name { get; set; } = string.Empty;
}

public class HolidayList : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();
}

public class Holiday : TenantEntity
{
    public Guid HolidayListId { get; set; }
    public HolidayList? HolidayList { get; set; }

    public DateOnly Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsWeekly { get; set; }
}
