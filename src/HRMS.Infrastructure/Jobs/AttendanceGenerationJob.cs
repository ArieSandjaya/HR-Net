using HRMS.Application.Attendance;
using HRMS.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HRMS.Infrastructure.Jobs;

/// <summary>
/// Daily recurring job (registered via Hangfire's RecurringJob in Program.cs) that aggregates
/// every active tenant's employee checkins into AttendanceRecords for the previous day.
///
/// Background jobs run outside any HTTP request/Blazor circuit, so there's no "logged-in user"
/// to derive a tenant from the way HrmsDbContext normally does via ITenantProvider. This job
/// works around that by explicitly iterating tenants and, for each one, creating a fresh DI
/// scope and calling ITenantProvider.SetTenant(...) before resolving IAttendanceService —
/// pinning that scope's HrmsDbContext to the correct tenant for its duration.
/// </summary>
public class AttendanceGenerationJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AttendanceGenerationJob> _logger;

    public AttendanceGenerationJob(IServiceScopeFactory scopeFactory, ILogger<AttendanceGenerationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunForAllTenantsAsync()
    {
        var targetDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));

        List<Guid> tenantIds;
        using (var listScope = _scopeFactory.CreateScope())
        {
            var db = listScope.ServiceProvider.GetRequiredService<Persistence.HrmsDbContext>();
            tenantIds = await db.Tenants.Where(t => t.IsActive).Select(t => t.Id).ToListAsync();
        }

        foreach (var tenantId in tenantIds)
        {
            using var scope = _scopeFactory.CreateScope();
            try
            {
                var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
                tenantProvider.SetTenant(tenantId);

                var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
                var processed = await attendanceService.GenerateAttendanceForAllEmployeesAsync(targetDate);

                _logger.LogInformation(
                    "Attendance generation for tenant {TenantId} on {Date}: {Count} employees processed.",
                    tenantId, targetDate, processed);
            }
            catch (Exception ex)
            {
                // One tenant failing shouldn't stop the rest from being processed.
                _logger.LogError(ex, "Attendance generation failed for tenant {TenantId} on {Date}.", tenantId, targetDate);
            }
        }
    }
}
