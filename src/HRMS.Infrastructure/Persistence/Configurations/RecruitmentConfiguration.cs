using HRMS.Domain.Entities.Recruitment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRMS.Infrastructure.Persistence.Configurations;

public class RecruitmentConfiguration :
    IEntityTypeConfiguration<JobOpening>,
    IEntityTypeConfiguration<JobApplicant>,
    IEntityTypeConfiguration<Interview>,
    IEntityTypeConfiguration<InterviewFeedback>,
    IEntityTypeConfiguration<JobOffer>
{
    public void Configure(EntityTypeBuilder<JobOpening> builder)
    {
        builder.Property(o => o.JobTitle).HasMaxLength(200).IsRequired();

        builder.HasOne(o => o.Department).WithMany().HasForeignKey(o => o.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Designation).WithMany().HasForeignKey(o => o.DesignationId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<JobApplicant> builder)
    {
        builder.Property(a => a.ApplicantName).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(256).IsRequired();

        builder.HasOne(a => a.JobOpening)
            .WithMany(o => o.Applicants)
            .HasForeignKey(a => a.JobOpeningId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<Interview> builder)
    {
        builder.HasOne(i => i.JobApplicant).WithMany().HasForeignKey(i => i.JobApplicantId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(i => i.InterviewerEmployee).WithMany().HasForeignKey(i => i.InterviewerEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<InterviewFeedback> builder)
    {
        builder.HasOne(f => f.Interview)
            .WithMany(i => i.Feedbacks)
            .HasForeignKey(f => f.InterviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.InterviewerEmployee).WithMany().HasForeignKey(f => f.InterviewerEmployeeId).OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<JobOffer> builder)
    {
        builder.HasOne(o => o.JobApplicant).WithMany().HasForeignKey(o => o.JobApplicantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Designation).WithMany().HasForeignKey(o => o.DesignationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(o => o.Department).WithMany().HasForeignKey(o => o.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
