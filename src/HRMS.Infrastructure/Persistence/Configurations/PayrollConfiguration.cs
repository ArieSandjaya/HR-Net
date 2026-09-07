using HRMS.Domain.Entities.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRMS.Infrastructure.Persistence.Configurations;

public class PayrollConfiguration :
    IEntityTypeConfiguration<SalaryComponent>,
    IEntityTypeConfiguration<SalaryStructure>,
    IEntityTypeConfiguration<SalaryStructureComponent>,
    IEntityTypeConfiguration<SalaryStructureAssignment>,
    IEntityTypeConfiguration<PayrollEntry>,
    IEntityTypeConfiguration<SalarySlip>,
    IEntityTypeConfiguration<SalarySlipComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(150).IsRequired();
    }

    public void Configure(EntityTypeBuilder<SalaryStructure> builder)
    {
        builder.Property(s => s.Name).HasMaxLength(150).IsRequired();
        builder.HasOne(s => s.Company).WithMany().HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SalaryStructureComponent> builder)
    {
        builder.HasOne(sc => sc.SalaryStructure)
            .WithMany(s => s.Components)
            .HasForeignKey(sc => sc.SalaryStructureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sc => sc.SalaryComponent).WithMany().HasForeignKey(sc => sc.SalaryComponentId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SalaryStructureAssignment> builder)
    {
        builder.HasIndex(a => new { a.EmployeeId, a.FromDate });
        builder.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.SalaryStructure).WithMany().HasForeignKey(a => a.SalaryStructureId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<PayrollEntry> builder)
    {
        builder.HasOne(e => e.Company).WithMany().HasForeignKey(e => e.CompanyId).OnDelete(DeleteBehavior.Restrict);
        // One payroll run per company per exact period — mirrors the duplicate-run guard in PayrollEntryService.
        builder.HasIndex(e => new { e.TenantId, e.CompanyId, e.PeriodStart, e.PeriodEnd }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<SalarySlip> builder)
    {
        builder.HasOne(s => s.PayrollEntry)
            .WithMany(e => e.SalarySlips)
            .HasForeignKey(s => s.PayrollEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Employee).WithMany().HasForeignKey(s => s.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<SalarySlipComponent> builder)
    {
        builder.HasOne(c => c.SalarySlip)
            .WithMany(s => s.Components)
            .HasForeignKey(c => c.SalarySlipId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
