using HRMS.Domain.Entities.Performance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRMS.Infrastructure.Persistence.Configurations;

public class PerformanceConfiguration :
    IEntityTypeConfiguration<AppraisalCycle>,
    IEntityTypeConfiguration<KRA>,
    IEntityTypeConfiguration<Goal>,
    IEntityTypeConfiguration<Appraisal>,
    IEntityTypeConfiguration<AppraisalFeedback>
{
    public void Configure(EntityTypeBuilder<AppraisalCycle> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
        builder.HasOne(c => c.Company).WithMany().HasForeignKey(c => c.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<KRA> builder)
    {
        builder.Property(k => k.Title).HasMaxLength(150).IsRequired();
    }

    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.HasOne(g => g.Employee).WithMany().HasForeignKey(g => g.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.AppraisalCycle).WithMany().HasForeignKey(g => g.AppraisalCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(g => g.KRA).WithMany().HasForeignKey(g => g.KRAId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<Appraisal> builder)
    {
        // One appraisal per employee per cycle — mirrors the duplicate guard in AppraisalService.
        builder.HasIndex(a => new { a.TenantId, a.EmployeeId, a.AppraisalCycleId }).IsUnique();

        builder.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.AppraisalCycle).WithMany().HasForeignKey(a => a.AppraisalCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ReviewedBy).WithMany().HasForeignKey(a => a.ReviewedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<AppraisalFeedback> builder)
    {
        builder.HasOne(f => f.Appraisal)
            .WithMany(a => a.Feedbacks)
            .HasForeignKey(f => f.AppraisalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.ReviewerEmployee).WithMany().HasForeignKey(f => f.ReviewerEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
