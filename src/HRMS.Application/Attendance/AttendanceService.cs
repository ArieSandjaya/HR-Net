using HRMS.Application.Common.Interfaces;
using HRMS.Application.Common.Models;
using HRMS.Domain.Common;
using HRMS.Domain.Entities.Attendance;
using HRMS.Domain.Entities.Employees;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Enums;

namespace HRMS.Application.Attendance;

public class AttendanceService : IAttendanceService
{
    private readonly IUnitOfWork _uow;

    public AttendanceService(IUnitOfWork uow) => _uow = uow;

    public async Task<Result> CheckInAsync(Guid employeeId, double? latitude, double? longitude, string? deviceId, CancellationToken ct = default)
    {
        if (await IsCurrentlyCheckedInAsync(employeeId, ct))
            return Result.Failure("Anda sudah check-in. Silakan check-out terlebih dahulu.");

        await _uow.Repository<EmployeeCheckin>().AddAsync(new EmployeeCheckin
        {
            EmployeeId = employeeId,
            Timestamp = DateTime.Now,
            IsCheckIn = true,
            Latitude = latitude,
            Longitude = longitude,
            DeviceId = deviceId
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> CheckOutAsync(Guid employeeId, double? latitude, double? longitude, string? deviceId, CancellationToken ct = default)
    {
        if (!await IsCurrentlyCheckedInAsync(employeeId, ct))
            return Result.Failure("Anda belum check-in hari ini.");

        await _uow.Repository<EmployeeCheckin>().AddAsync(new EmployeeCheckin
        {
            EmployeeId = employeeId,
            Timestamp = DateTime.Now,
            IsCheckIn = false,
            Latitude = latitude,
            Longitude = longitude,
            DeviceId = deviceId
        }, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<List<CheckinDto>> GetTodayCheckinsAsync(Guid employeeId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var checkins = await GetCheckinsForDateAsync(employeeId, today, ct);
        return checkins.Select(c => new CheckinDto(c.Id, c.Timestamp, c.IsCheckIn, c.Latitude, c.Longitude)).ToList();
    }

    public async Task<AttendanceRecordDto> GenerateAttendanceForDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default)
    {
        var employee = await _uow.Repository<Employee>().GetByIdAsync(employeeId, ct);

        // BUG FIX: previously holidays weren't considered at all, so an employee with zero
        // checkins on a weekend/company holiday would be marked Absent by the logic below.
        // Check the employee's assigned HolidayList first and short-circuit if today is one.
        if (employee?.HolidayListId is Guid holidayListId)
        {
            var isHoliday = (await _uow.Repository<Holiday>().FindAsync(
                h => h.HolidayListId == holidayListId && h.Date == date, ct)).Count > 0;

            if (isHoliday)
            {
                var holidayRecord = await UpsertAttendanceRecordAsync(employeeId, date, AttendanceStatus.Holiday, null, ct);
                return new AttendanceRecordDto(holidayRecord.Id, holidayRecord.EmployeeId, employee.FullName,
                    holidayRecord.AttendanceDate, holidayRecord.Status, holidayRecord.WorkingHours, holidayRecord.Remarks);
            }
        }

        var checkins = await GetCheckinsForDateAsync(employeeId, date, ct);

        // Pair IN/OUT punches chronologically to compute total working hours.
        var totalWorked = TimeSpan.Zero;
        DateTime? openCheckIn = null;
        foreach (var c in checkins)
        {
            if (c.IsCheckIn)
            {
                openCheckIn = c.Timestamp;
            }
            else if (openCheckIn.HasValue)
            {
                totalWorked += c.Timestamp - openCheckIn.Value;
                openCheckIn = null;
            }
        }

        // Approved leave for this date takes priority over checkin-derived status.
        var leaveOnThisDate = await _uow.Repository<LeaveApplication>().FindAsync(
            la => la.EmployeeId == employeeId
                  && la.Status == LeaveApplicationStatus.Approved
                  && la.FromDate <= date && la.ToDate >= date, ct);

        var status = leaveOnThisDate.Count > 0
            ? AttendanceStatus.OnLeave
            : checkins.Count == 0
                ? AttendanceStatus.Absent
                : totalWorked >= TimeSpan.FromHours(4)
                    ? AttendanceStatus.Present
                    : AttendanceStatus.HalfDay;

        var record = await UpsertAttendanceRecordAsync(employeeId, date, status, checkins.Count > 0 ? totalWorked : null, ct);

        return new AttendanceRecordDto(record.Id, record.EmployeeId, employee?.FullName,
            record.AttendanceDate, record.Status, record.WorkingHours, record.Remarks);
    }

    private async Task<AttendanceRecord> UpsertAttendanceRecordAsync(Guid employeeId, DateOnly date, AttendanceStatus status, TimeSpan? workingHours, CancellationToken ct)
    {
        var repo = _uow.Repository<AttendanceRecord>();
        var existing = await repo.FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.AttendanceDate == date, ct);

        if (existing is null)
        {
            existing = new AttendanceRecord { EmployeeId = employeeId, AttendanceDate = date };
            await repo.AddAsync(existing, ct);
        }

        existing.Status = status;
        existing.WorkingHours = workingHours;
        existing.DocStatus = DocumentStatus.Submitted;
        repo.Update(existing);

        await _uow.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<int> GenerateAttendanceForAllEmployeesAsync(DateOnly date, CancellationToken ct = default)
    {
        var employees = await _uow.Repository<Employee>().FindAsync(e => e.Status == EmployeeStatus.Active, ct);
        foreach (var employee in employees)
            await GenerateAttendanceForDateAsync(employee.Id, date, ct);

        return employees.Count;
    }

    public async Task<List<AttendanceRecordDto>> GetAttendanceRecordsAsync(Guid? employeeId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
    {
        var records = employeeId.HasValue
            ? await _uow.Repository<AttendanceRecord>().FindAsync(
                a => a.EmployeeId == employeeId.Value && a.AttendanceDate >= fromDate && a.AttendanceDate <= toDate, ct)
            : await _uow.Repository<AttendanceRecord>().FindAsync(
                a => a.AttendanceDate >= fromDate && a.AttendanceDate <= toDate, ct);

        var employees = await _uow.Repository<Employee>().GetAllAsync(ct);
        var employeeNames = employees.ToDictionary(e => e.Id, e => e.FullName);

        return records
            .OrderByDescending(a => a.AttendanceDate)
            .Select(a => new AttendanceRecordDto(
                a.Id, a.EmployeeId, employeeNames.GetValueOrDefault(a.EmployeeId),
                a.AttendanceDate, a.Status, a.WorkingHours, a.Remarks))
            .ToList();
    }

    private async Task<List<EmployeeCheckin>> GetCheckinsForDateAsync(Guid employeeId, DateOnly date, CancellationToken ct)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var checkins = await _uow.Repository<EmployeeCheckin>().FindAsync(
            c => c.EmployeeId == employeeId && c.Timestamp >= start && c.Timestamp < end, ct);
        return checkins.OrderBy(c => c.Timestamp).ToList();
    }

    private async Task<bool> IsCurrentlyCheckedInAsync(Guid employeeId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var checkins = await GetCheckinsForDateAsync(employeeId, today, ct);
        return checkins.Count > 0 && checkins[^1].IsCheckIn;
    }
}
