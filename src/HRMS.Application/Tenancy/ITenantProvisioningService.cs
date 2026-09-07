using HRMS.Application.Common.Models;

namespace HRMS.Application.Tenancy;

public record RegisterTenantRequest(
    string TenantName,
    string TenantCode,
    string AdminFullName,
    string AdminEmail,
    string AdminPassword
);

/// <summary>
/// Onboards a brand-new customer organization: creates the Tenant record, the first
/// Administrator user scoped to it, and seeds minimal default reference data
/// (default Company, Departments, Designations, EmploymentTypes, LeaveTypes) so the
/// tenant isn't empty on first login. Equivalent to Frappe's "New Site" + fixtures install,
/// but done per-tenant instead of per-database since we use the shared-database model.
/// </summary>
public interface ITenantProvisioningService
{
    Task<Result<Guid>> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken ct = default);
}
