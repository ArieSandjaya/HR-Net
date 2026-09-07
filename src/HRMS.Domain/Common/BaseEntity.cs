namespace HRMS.Domain.Common;

/// <summary>
/// Base class for every aggregate/entity in the system.
/// Mirrors Frappe's implicit doctype fields: name, owner, creation, modified, modified_by.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }
    public string? ModifiedBy { get; set; }

    /// <summary>Soft delete flag — records are never hard-deleted for audit purposes.</summary>
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Marks an entity as belonging to a single Tenant (customer organization) in the
/// shared-database multi-tenancy model. HrmsDbContext applies a global query filter
/// to every entity implementing this interface, and auto-stamps TenantId on insert —
/// so individual services never need to remember to filter/set it manually.
/// </summary>
public interface IMustHaveTenant
{
    Guid TenantId { get; set; }
}

/// <summary>Base class for entities scoped to a tenant. Use this instead of BaseEntity
/// for anything that belongs to a customer organization (i.e. everything except Tenant itself).</summary>
public abstract class TenantEntity : BaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; set; }
}

/// <summary>
/// Base class for entities that go through a document workflow
/// (Draft -> Submitted -> Cancelled), matching Frappe's docstatus concept.
/// </summary>
public abstract class SubmittableEntity : TenantEntity
{
    public DocumentStatus DocStatus { get; set; } = DocumentStatus.Draft;
}

public enum DocumentStatus
{
    Draft = 0,
    Submitted = 1,
    Cancelled = 2
}
