using HRMS.Domain.Common;

namespace HRMS.Domain.Entities.Tenancy;

/// <summary>
/// Represents one customer organization sharing this HRMS deployment.
/// Deliberately extends BaseEntity (not TenantEntity) — a tenant does not belong to itself.
/// One Tenant can still contain multiple Companies internally (multi-company within one customer),
/// same as ERPNext's multi-company support — Tenant is one level above that.
/// </summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Short unique code, e.g. "ACME" — used for display and potential future subdomain routing.</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Simple plan/tier gate for future use (seat limits, feature flags, etc.).</summary>
    public string Plan { get; set; } = "Standard";

    /// <summary>Monotonically increasing counter used to generate sequential employee numbers
    /// (EMP-00001, EMP-00002, ...) scoped to this tenant. A persistent counter — rather than
    /// counting current rows — is required because Employees are soft-deleted, not removed:
    /// counting rows would regenerate an already-used (but now hidden) number and collide with
    /// the unique (TenantId, EmployeeNumber) index.</summary>
    public int EmployeeSequence { get; set; }
}
