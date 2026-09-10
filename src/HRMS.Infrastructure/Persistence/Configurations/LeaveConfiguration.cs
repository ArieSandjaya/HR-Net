using HRMS.Domain.Entities.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRMS.Infrastructure.Persistence.Configurations;

/// <summary>
/// BUG FIX: this configuration class never existed — every other module (Attendance,
/// Recruitment, Payroll, Performance, Expense, Separation) got an explicit Fluent API
/// configuration, but the Leave module (built in Phase 1, before that convention was
/// established) never did. LeaveApplication has two navigation properties to Employee
/// (Employee via EmployeeId, ApprovedBy via ApprovedByEmployeeId), and without an explicit
/// HasForeignKey on each, EF Core's convention-based relationship discovery cannot determine
/// which FK column belongs to which relationship. That throws:
///   "Both relationships between 'LeaveApplication.ApprovedBy' and 'Employee' and between
///    'LeaveApplication.Employee' and 'Employee' could use {'EmployeeId'} as the foreign key..."
/// at model-build time — i.e. the first time the DbContext actually touches a real database,
/// which is exactly why this was never caught in the sandbox (no SQL Server available there
/// to ever trigger model validation).
/// </summary>
public class LeaveConfiguration :
    IEntityTypeConfiguration<LeaveType>,
    IEntityTypeConfiguration<LeaveAllocation>,
    IEntityTypeConfiguration<LeaveApplication>,
    IEntityTypeConfiguration<LeaveLedgerEntry>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
    }

    public void Configure(EntityTypeBuilder<LeaveAllocation> builder)
    {
        builder.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.LeaveType).WithMany().HasForeignKey(a => a.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<LeaveApplication> builder)
    {
        // The actual fix: explicit FK for each of the two Employee relationships.
        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ApprovedBy)
            .WithMany()
            .HasForeignKey(a => a.ApprovedByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.LeaveType)
            .WithMany()
            .HasForeignKey(a => a.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<LeaveLedgerEntry> builder)
    {
        // Deliberately has no navigation properties (append-only historical ledger, see
        // entity XML doc) — EmployeeId/LeaveTypeId are plain scalar columns, not FKs, so
        // there's no relationship to configure here. Just index for the query pattern
        // LeaveApplicationService actually uses (balance lookup by employee+leave type+date).
        builder.HasIndex(e => new { e.EmployeeId, e.LeaveTypeId, e.TransactionDate });
    }
}
