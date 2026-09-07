using System.Linq.Expressions;
using HRMS.Domain.Common;

namespace HRMS.Application.Common.Interfaces;

/// <summary>
/// Generic repository abstraction. Application layer depends only on this interface,
/// never on EF Core directly — keeps business logic testable and framework-agnostic.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<T>> GetAllAsync(CancellationToken ct = default);
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

/// <summary>Coordinates repositories under a single transaction (SaveChanges = commit).</summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Provides the currently authenticated user for auditing (implemented in Web layer via HttpContext / AuthenticationStateProvider).</summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    Guid? EmployeeId { get; }
    bool IsInRole(string role);
}

/// <summary>
/// Resolves the tenant of the current request/circuit. Implemented in the Web layer,
/// backed by the "TenantId" claim set on the user's ClaimsPrincipal at login
/// (see ApplicationUserClaimsPrincipalFactory in Infrastructure).
/// HrmsDbContext injects this to apply the global per-tenant query filter and to
/// auto-stamp TenantId on newly inserted entities.
/// </summary>
public interface ITenantProvider
{
    /// <summary>Guid.Empty when no tenant is resolved yet (e.g. anonymous request, tenant registration flow).</summary>
    Guid TenantId { get; }

    /// <summary>
    /// Explicitly overrides the resolved tenant for the remainder of this DI scope, taking
    /// priority over claims-based resolution. Background jobs (Hangfire) run outside any
    /// authenticated HTTP/Blazor context — there's no user session to read a "TenantId" claim
    /// from — so a job that needs to process a specific tenant must call this first, within
    /// its own DI scope, before resolving any service that depends on HrmsDbContext.
    /// </summary>
    void SetTenant(Guid tenantId);
}
