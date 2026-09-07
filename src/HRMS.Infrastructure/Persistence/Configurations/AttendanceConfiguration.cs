using HRMS.Domain.Entities.Attendance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HRMS.Infrastructure.Persistence.Configurations;

public class AttendanceConfiguration :
    IEntityTypeConfiguration<EmployeeCheckin>,
    IEntityTypeConfiguration<AttendanceRecord>,
    IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeCheckin> builder)
    {
        // Frequent query pattern: "all checkins for employee X on date Y" (see AttendanceService).
        builder.HasIndex(c => new { c.EmployeeId, c.Timestamp });
    }

    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        // One attendance record per employee per day, scoped per tenant.
        builder.HasIndex(a => new { a.TenantId, a.EmployeeId, a.AttendanceDate }).IsUnique();
    }

    public void Configure(EntityTypeBuilder<ShiftAssignment> builder)
    {
        builder.HasIndex(sa => new { sa.EmployeeId, sa.StartDate });
    }
}
