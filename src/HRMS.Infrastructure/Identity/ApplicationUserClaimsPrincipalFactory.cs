using System.Security.Claims;
using HRMS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace HRMS.Infrastructure.Identity;

/// <summary>
/// Adds "TenantId" (and "EmployeeId" when linked) as claims on the user's ClaimsPrincipal
/// at sign-in time. These claims are what ITenantProvider/ICurrentUserService read in the
/// Web layer — this is the single place tenant context enters the authentication pipeline.
/// </summary>
public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options) : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim("TenantId", user.TenantId.ToString()));
        if (user.EmployeeId.HasValue)
            identity.AddClaim(new Claim("EmployeeId", user.EmployeeId.Value.ToString()));
        if (!string.IsNullOrWhiteSpace(user.FullName))
            identity.AddClaim(new Claim("FullName", user.FullName));

        return identity;
    }
}
