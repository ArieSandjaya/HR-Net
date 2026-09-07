using System.Security.Claims;
using HRMS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

namespace HRMS.Web.Services;

/// <summary>
/// Both services below resolve identity from AuthenticationStateProvider rather than
/// IHttpContextAccessor. This matters specifically for Blazor Server: once a component
/// renders with InteractiveServer mode, HttpContext becomes unreliable/null for the rest
/// of the SignalR circuit's lifetime, but AuthenticationStateProvider remains valid because
/// it captures the ClaimsPrincipal once at circuit start and holds onto it.
///
/// Both are registered Scoped (once per circuit), and cache the resolved ClaimsPrincipal
/// after the first (blocking) read — acceptable here since it happens at most once per
/// user session, not per request.
/// </summary>
public class TenantProvider : ITenantProvider
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private Guid? _cached;
    private Guid? _explicitOverride;

    public TenantProvider(AuthenticationStateProvider authStateProvider) => _authStateProvider = authStateProvider;

    /// <summary>Used by background jobs to pin this scope to a specific tenant — see
    /// ITenantProvider.SetTenant and Infrastructure/Jobs/AttendanceGenerationJob.</summary>
    public void SetTenant(Guid tenantId) => _explicitOverride = tenantId;

    public Guid TenantId
    {
        get
        {
            if (_explicitOverride.HasValue)
                return _explicitOverride.Value;

            if (_cached is null)
            {
                try
                {
                    var user = _authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;
                    var claim = user.FindFirst("TenantId")?.Value;
                    _cached = Guid.TryParse(claim, out var id) ? id : Guid.Empty;
                }
                catch
                {
                    // Resolved outside an active Blazor circuit (e.g. from a plain minimal API
                    // endpoint like /account/login, invoked before any circuit exists) —
                    // default to "no tenant" rather than letting DbContext construction fail.
                    _cached = Guid.Empty;
                }
            }
            return _cached.Value;
        }
    }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly AuthenticationStateProvider _authStateProvider;
    private ClaimsPrincipal? _cachedUser;

    public CurrentUserService(AuthenticationStateProvider authStateProvider) => _authStateProvider = authStateProvider;

    private ClaimsPrincipal User
    {
        get
        {
            if (_cachedUser is null)
            {
                try
                {
                    _cachedUser = _authStateProvider.GetAuthenticationStateAsync().GetAwaiter().GetResult().User;
                }
                catch
                {
                    _cachedUser = new ClaimsPrincipal(new ClaimsIdentity());
                }
            }
            return _cachedUser;
        }
    }

    public string? UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public string? UserName => User.FindFirst("FullName")?.Value ?? User.Identity?.Name;

    public Guid? EmployeeId
    {
        get
        {
            var claim = User.FindFirst("EmployeeId")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public bool IsInRole(string role) => User.IsInRole(role);
}
