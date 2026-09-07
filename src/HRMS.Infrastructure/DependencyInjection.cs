using HRMS.Application.Attendance;
using HRMS.Application.Common.Interfaces;
using HRMS.Application.Employees;
using HRMS.Application.Expense;
using HRMS.Application.Leave;
using HRMS.Application.Payroll;
using HRMS.Application.Performance;
using HRMS.Application.Recruitment;
using HRMS.Application.Separation;
using HRMS.Application.Tenancy;
using HRMS.Infrastructure.Identity;
using HRMS.Infrastructure.Jobs;
using HRMS.Infrastructure.Persistence;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HRMS.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers everything except ICurrentUserService/ITenantProvider — those two are
    /// Blazor-specific (backed by AuthenticationStateProvider) and registered in HRMS.Web's
    /// Program.cs instead, to keep this layer UI-framework-agnostic.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Set it in appsettings.json.");

        services.AddDbContext<HrmsDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(HrmsDbContext).Assembly.FullName)));

        // Self-hosted Identity (cookie-based auth) — no Azure AD / external IdP required.
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddEntityFrameworkStores<HrmsDbContext>()
            .AddDefaultTokenProviders();

        // Ensures TenantId/EmployeeId claims are present on every signed-in user's ClaimsPrincipal.
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Application services
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ILeaveApplicationService, LeaveApplicationService>();
        services.AddScoped<ILeaveAllocationService, LeaveAllocationService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IJobOpeningService, JobOpeningService>();
        services.AddScoped<IJobApplicantService, JobApplicantService>();
        services.AddScoped<IInterviewService, InterviewService>();
        services.AddScoped<IJobOfferService, JobOfferService>();
        services.AddScoped<ISalaryComponentService, SalaryComponentService>();
        services.AddScoped<ISalaryStructureService, SalaryStructureService>();
        services.AddScoped<IPayrollEntryService, PayrollEntryService>();
        services.AddScoped<IAppraisalCycleService, AppraisalCycleService>();
        services.AddScoped<IKraService, KraService>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IAppraisalService, AppraisalService>();
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<IExpenseClaimService, ExpenseClaimService>();
        services.AddScoped<IEmployeeAdvanceService, EmployeeAdvanceService>();
        services.AddScoped<ITravelRequestService, TravelRequestService>();
        services.AddScoped<IEmployeeSeparationService, EmployeeSeparationService>();
        services.AddScoped<IExitInterviewService, ExitInterviewService>();
        services.AddScoped<IFnFSettlementService, FnFSettlementService>();

        // Resolved directly by Hangfire (one instance per job execution, via its own DI scope).
        services.AddScoped<AttendanceGenerationJob>();

        // Background jobs, persisted in the same SQL Server instance (fully self-hosted,
        // no Azure Functions / cloud scheduler needed) — replaces Frappe's cron-based scheduler.
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true
            }));
        services.AddHangfireServer();

        return services;
    }
}
