using HRMS.Domain.Enums;

namespace HRMS.Application.Employees;

public record EmployeeDto(
    Guid Id,
    string EmployeeNumber,
    string FullName,
    string Email,
    string? MobileNumber,
    DateOnly DateOfJoining,
    EmployeeStatus Status,
    Guid DepartmentId,
    string? DepartmentName,
    Guid DesignationId,
    string? DesignationName,
    Guid? ReportsToEmployeeId,
    string? ReportsToName
);

public record CreateEmployeeRequest(
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly DateOfBirth,
    Gender Gender,
    string Email,
    string? MobileNumber,
    DateOnly DateOfJoining,
    Guid CompanyId,
    Guid DepartmentId,
    Guid DesignationId,
    Guid EmploymentTypeId,
    Guid? ReportsToEmployeeId,
    Guid? BranchId,
    Guid? HolidayListId
);

public record UpdateEmployeeRequest(
    Guid Id,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? MobileNumber,
    Guid DepartmentId,
    Guid DesignationId,
    Guid? ReportsToEmployeeId,
    EmployeeStatus Status
);
