using HRMS.Application.Common.Models;
using HRMS.Application.Tenancy;
using HRMS.Domain.Entities.Leave;
using HRMS.Domain.Entities.Organization;
using HRMS.Domain.Entities.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HRMS.Infrastructure.Persistence;

public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly HrmsDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public TenantProvisioningService(HrmsDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<Result<Guid>> RegisterTenantAsync(RegisterTenantRequest request, CancellationToken ct = default)
    {
        // BUG FIX: normalize BEFORE comparing/storing. Previously the raw (un-normalized)
        // input was checked against already-uppercased stored codes, so e.g. "acme" would
        // slip past this friendly check when "ACME" already existed, only to fail later with
        // a raw database unique-constraint exception instead of the intended message below.
        var normalizedCode = request.TenantCode.Trim().ToUpperInvariant();
        var codeTaken = await _db.Tenants.AnyAsync(t => t.Code == normalizedCode, ct);
        if (codeTaken)
            return Result<Guid>.Failure($"Kode tenant '{normalizedCode}' sudah dipakai. Gunakan kode lain.");

        var emailTaken = await _userManager.FindByEmailAsync(request.AdminEmail);
        if (emailTaken is not null)
            return Result<Guid>.Failure($"Email '{request.AdminEmail}' sudah terdaftar.");

        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // 1. Create the Tenant itself (not tenant-scoped, so TenantId auto-stamping doesn't apply here).
            var tenant = new Tenant { Name = request.TenantName, Code = normalizedCode };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(ct);

            // 2. Seed minimal default reference data for this tenant, with TenantId set explicitly
            //    (there's no "current" tenant context yet during registration — CurrentTenantId
            //    would resolve to Guid.Empty since nobody is logged in, so we set it directly).
            var company = new Company { Name = request.TenantName, Abbreviation = normalizedCode, TenantId = tenant.Id, DefaultCurrency = "IDR" };
            _db.Companies.Add(company);

            _db.Departments.AddRange(
                new Department { Name = "Human Resources", CompanyId = company.Id, TenantId = tenant.Id },
                new Department { Name = "Engineering", CompanyId = company.Id, TenantId = tenant.Id },
                new Department { Name = "Finance", CompanyId = company.Id, TenantId = tenant.Id });

            _db.Designations.AddRange(
                new Designation { Name = "Staff", TenantId = tenant.Id },
                new Designation { Name = "Manager", TenantId = tenant.Id });

            _db.EmploymentTypes.Add(new EmploymentType { Name = "Full-time", TenantId = tenant.Id });

            _db.LeaveTypes.AddRange(
                new LeaveType { Name = "Cuti Tahunan", IsPaidLeave = true, IsCarryForward = true, MaxCarryForwardDays = 6, MaxLeavesAllowedPerYear = 12, TenantId = tenant.Id },
                new LeaveType { Name = "Cuti Sakit", IsPaidLeave = true, AllowNegativeBalance = true, TenantId = tenant.Id },
                new LeaveType { Name = "Cuti Tanpa Gaji", IsPaidLeave = false, AllowNegativeBalance = true, TenantId = tenant.Id });

            await _db.SaveChangesAsync(ct);

            // 3. Create the first Administrator user for this tenant.
            var adminUser = new ApplicationUser
            {
                UserName = request.AdminEmail,
                Email = request.AdminEmail,
                FullName = request.AdminFullName,
                TenantId = tenant.Id,
                EmailConfirmed = true
            };

            var createResult = await _userManager.CreateAsync(adminUser, request.AdminPassword);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(ct);
                return Result<Guid>.Failure(createResult.Errors.Select(e => e.Description).ToArray());
            }

            await _userManager.AddToRoleAsync(adminUser, "Administrator");

            await transaction.CommitAsync(ct);
            return Result<Guid>.Success(tenant.Id);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return Result<Guid>.Failure($"Gagal membuat tenant: {ex.Message}");
        }
    }
}
