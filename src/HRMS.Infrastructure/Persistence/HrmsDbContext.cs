using System.Reflection;
using HRMS.Application.Common.Interfaces;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Expense;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Entities.Payroll;
using HRMS.Domain.Entities.Performance;
using HRMS.Domain.Entities.Recruitment;
using HRMS.Domain.Entities.Separation;
using HRMS.Domain.Entities.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HRMS.Infrastructure.Persistence;

/// <summary>
/// Application user for ASP.NET Identity. Kept separate from the Employee entity
/// (Employee.UserId links to this.Id) so HR data survives even if login accounts change.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public Guid? EmployeeId { get; set; }

    /// <summary>Every user belongs to exactly one tenant (single-tenant-per-user model,
    /// resolved once at login and carried as a claim for the rest of the session).</summary>
    public Guid TenantId { get; set; }

    public string? FullName { get; set; }
}

public class HrmsDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantProvider _tenantProvider;

    public HrmsDbContext(DbContextOptions<HrmsDbContext> options, ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    /// <summary>Captured once per DbContext instance (i.e. once per request/circuit scope) and
    /// referenced by the query filter lambdas below via closure over `this`.</summary>
    private Guid CurrentTenantId => _tenantProvider.TenantId;

    // Tenancy
    public DbSet<Tenant> Tenants => Set<Tenant>();

    // Organization
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Designation> Designations => Set<Designation>();
    public DbSet<EmploymentType> EmploymentTypes => Set<EmploymentType>();
    public DbSet<HolidayList> HolidayLists => Set<HolidayList>();
    public DbSet<Holiday> Holidays => Set<Holiday>();

    // Employees
    public DbSet<Employee> Employees => Set<Employee>();

    // Leave
    public DbSet<LeaveType> LeaveTypes => Set<LeaveType>();
    public DbSet<LeaveAllocation> LeaveAllocations => Set<LeaveAllocation>();
    public DbSet<LeaveApplication> LeaveApplications => Set<LeaveApplication>();
    public DbSet<LeaveLedgerEntry> LeaveLedgerEntries => Set<LeaveLedgerEntry>();

    // Attendance
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<EmployeeCheckin> EmployeeCheckins => Set<EmployeeCheckin>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    // Recruitment
    public DbSet<JobOpening> JobOpenings => Set<JobOpening>();
    public DbSet<JobApplicant> JobApplicants => Set<JobApplicant>();
    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<InterviewFeedback> InterviewFeedbacks => Set<InterviewFeedback>();
    public DbSet<JobOffer> JobOffers => Set<JobOffer>();

    // Payroll
    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<SalaryStructureComponent> SalaryStructureComponents => Set<SalaryStructureComponent>();
    public DbSet<SalaryStructureAssignment> SalaryStructureAssignments => Set<SalaryStructureAssignment>();
    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();
    public DbSet<SalarySlip> SalarySlips => Set<SalarySlip>();
    public DbSet<SalarySlipComponent> SalarySlipComponents => Set<SalarySlipComponent>();

    // Performance
    public DbSet<AppraisalCycle> AppraisalCycles => Set<AppraisalCycle>();
    public DbSet<KRA> KRAs => Set<KRA>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Appraisal> Appraisals => Set<Appraisal>();
    public DbSet<AppraisalFeedback> AppraisalFeedbacks => Set<AppraisalFeedback>();

    // Expense & Travel
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<ExpenseClaim> ExpenseClaims => Set<ExpenseClaim>();
    public DbSet<ExpenseClaimDetail> ExpenseClaimDetails => Set<ExpenseClaimDetail>();
    public DbSet<EmployeeAdvance> EmployeeAdvances => Set<EmployeeAdvance>();
    public DbSet<TravelRequest> TravelRequests => Set<TravelRequest>();

    // Exit & Separation
    public DbSet<EmployeeSeparation> EmployeeSeparations => Set<EmployeeSeparation>();
    public DbSet<ExitInterview> ExitInterviews => Set<ExitInterview>();
    public DbSet<FullAndFinalSettlement> FullAndFinalSettlements => Set<FullAndFinalSettlement>();
    public DbSet<FnFComponent> FnFComponents => Set<FnFComponent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // required first, sets up Identity tables

        builder.ApplyConfigurationsFromAssembly(typeof(HrmsDbContext).Assembly);

        // Prevent decimal-to-money silent truncation across the model.
        foreach (var property in builder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(18);
            property.SetScale(4);
        }

        // ---- Multi-tenant isolation ----
        // Every entity implementing IMustHaveTenant automatically gets a query filter that
        // (a) restricts rows to the current tenant, and (b) hides soft-deleted rows.
        // This is applied via reflection so new modules get tenant isolation "for free"
        // just by inheriting TenantEntity — nobody has to remember to add a filter.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (typeof(IMustHaveTenant).IsAssignableFrom(entityType.ClrType))
            {
                ConfigureTenantQueryFilterMethod
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] { builder });
            }
        }
    }

    private static readonly MethodInfo ConfigureTenantQueryFilterMethod = typeof(HrmsDbContext)
        .GetMethod(nameof(ConfigureTenantQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private void ConfigureTenantQueryFilter<T>(ModelBuilder builder) where T : TenantEntity
    {
        builder.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId && !e.IsDeleted);
    }

    /// <summary>Auto-stamps TenantId on every newly inserted tenant-scoped entity, unless it was
    /// already set explicitly (used by tenant provisioning, which writes data before any
    /// "current" tenant/user context exists).</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTenantId();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenantId();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampTenantId()
    {
        foreach (var entry in ChangeTracker.Entries<IMustHaveTenant>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Entity.TenantId = CurrentTenantId;
            }
        }
    }
}
