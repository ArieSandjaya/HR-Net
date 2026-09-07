using HRMS.Domain.Entities.Expense;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRMS.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration :
    IEntityTypeConfiguration<ExpenseCategory>,
    IEntityTypeConfiguration<ExpenseClaim>,
    IEntityTypeConfiguration<ExpenseClaimDetail>,
    IEntityTypeConfiguration<EmployeeAdvance>,
    IEntityTypeConfiguration<TravelRequest>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
    }

    public void Configure(EntityTypeBuilder<ExpenseClaim> builder)
    {
        builder.HasOne(c => c.Employee).WithMany().HasForeignKey(c => c.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ApprovedBy).WithMany().HasForeignKey(c => c.ApprovedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.SettlesAdvance).WithMany().HasForeignKey(c => c.SettlesAdvanceId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ExpenseClaimDetail> builder)
    {
        builder.HasOne(d => d.ExpenseClaim)
            .WithMany(c => c.Details)
            .HasForeignKey(d => d.ExpenseClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.ExpenseCategory).WithMany().HasForeignKey(d => d.ExpenseCategoryId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<EmployeeAdvance> builder)
    {
        builder.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ApprovedBy).WithMany().HasForeignKey(a => a.ApprovedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<TravelRequest> builder)
    {
        builder.Property(r => r.Destination).HasMaxLength(200).IsRequired();
        builder.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ApprovedBy).WithMany().HasForeignKey(r => r.ApprovedByEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
