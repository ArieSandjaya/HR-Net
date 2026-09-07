using HRMS.Domain.Entities.Separation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRMS.Infrastructure.Persistence.Configurations;

public class SeparationConfiguration :
    IEntityTypeConfiguration<EmployeeSeparation>,
    IEntityTypeConfiguration<ExitInterview>,
    IEntityTypeConfiguration<FullAndFinalSettlement>,
    IEntityTypeConfiguration<FnFComponent>
{
    public void Configure(EntityTypeBuilder<EmployeeSeparation> builder)
    {
        builder.HasOne(s => s.Employee).WithMany().HasForeignKey(s => s.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.ApprovedBy).WithMany().HasForeignKey(s => s.ApprovedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ExitInterview> builder)
    {
        builder.HasOne(i => i.EmployeeSeparation)
            .WithMany(s => s.ExitInterviews)
            .HasForeignKey(i => i.EmployeeSeparationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.InterviewerEmployee).WithMany().HasForeignKey(i => i.InterviewerEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<FullAndFinalSettlement> builder)
    {
        // One settlement per separation — mirrors the duplicate guard in FnFSettlementService.
        builder.HasIndex(f => new { f.TenantId, f.EmployeeSeparationId }).IsUnique();

        builder.HasOne(f => f.EmployeeSeparation).WithMany().HasForeignKey(f => f.EmployeeSeparationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(f => f.Employee).WithMany().HasForeignKey(f => f.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<FnFComponent> builder)
    {
        builder.HasOne(c => c.FullAndFinalSettlement)
            .WithMany(f => f.Components)
            .HasForeignKey(c => c.FullAndFinalSettlementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
