using HRMS.Domain.Common;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Enums;

namespace HRMS.Domain.Entities.Employees;

public class Employee : TenantEntity
{
    /// <summary>Human-readable employee number, e.g. "EMP-0001" (auto-generated).</summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string FullName => string.Join(" ", new[] { FirstName, MiddleName, LastName }
        .Where(s => !string.IsNullOrWhiteSpace(s)));

    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public MaritalStatus? MaritalStatus { get; set; }

    public string Email { get; set; } = string.Empty;
    public string? PersonalEmail { get; set; }
    public string? MobileNumber { get; set; }
    public string? CurrentAddress { get; set; }
    public string? PermanentAddress { get; set; }

    public DateOnly DateOfJoining { get; set; }
    public DateOnly? RelievingDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    // Organizational relations
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }

    public Guid DesignationId { get; set; }
    public Designation? Designation { get; set; }

    public Guid EmploymentTypeId { get; set; }
    public EmploymentType? EmploymentType { get; set; }

    public Guid? ReportsToEmployeeId { get; set; }
    public Employee? ReportsTo { get; set; }

    public Guid? HolidayListId { get; set; }
    public HolidayList? HolidayList { get; set; }

    /// <summary>Linked login account (1:1 with ASP.NET Identity user), nullable for employees without portal access.</summary>
    public string? UserId { get; set; }

    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();
}
