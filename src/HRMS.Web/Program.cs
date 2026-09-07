using System.Threading.RateLimiting;
using HRMS.Application.Common.Interfaces;
using HRMS.Infrastructure;
using HRMS.Infrastructure.Persistence;
using HRMS.Web.Components;
using HRMS.Web.Services;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

// Blazor-specific implementations of the tenant/user context abstractions
// (see comments in HRMS.Web/Services/TenantProvider.cs for why these live here
// and not in HRMS.Infrastructure).
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Cookie hardening: short-ish sliding expiration, strict same-site, and explicit
// redirect paths (HttpOnly + Secure are already Identity's secure-by-default).
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Rate limit the login endpoint specifically — mitigates credential-stuffing / brute force
// against a self-hosted deployment that may not sit behind an external WAF.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsync("Terlalu banyak percobaan masuk. Coba lagi dalam beberapa saat.", ct);
    };
});

var app = builder.Build();

// ---- Apply pending EF Core migrations automatically on startup (self-hosted convenience) ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HrmsDbContext>();
    db.Database.Migrate();

    // Seed global Identity roles (shared across all tenants — a role name like
    // "HR Manager" means the same thing regardless of which tenant the user belongs to).
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Administrator", "HR Manager", "HR User", "Employee" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}

// ---- HTTP pipeline ----
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseRateLimiter();

// ---- Auth endpoints ----
// Plain minimal-API endpoints (not Blazor components) so sign-in/sign-out can issue
// Set-Cookie + redirect via a normal HTTP response — not possible from an already-open
// Blazor Server SignalR circuit. See Components/Pages/Account/Login.razor for the form
// that posts here. Both endpoints require the antiforgery token embedded by
// <AntiforgeryToken /> in their respective forms (Login.razor, NavMenu.razor) —
// this is the ASP.NET Core 8 default for state-changing minimal API endpoints once
// AddAntiforgery/UseAntiforgery are configured, so no extra opt-in is needed here.
app.MapPost("/account/login", async (
        HttpContext http,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        HrmsDbContext db) =>
    {
        var form = await http.Request.ReadFormAsync();
        var email = form["email"].ToString();
        var password = form["password"].ToString();
        var returnUrl = form["returnUrl"].ToString();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
            return Results.Redirect("/login?error=1");

        // Block login for a deactivated tenant even if the credentials themselves are correct —
        // e.g. a customer whose subscription lapsed shouldn't be able to reach their data.
        var tenant = await db.Tenants.FindAsync(user.TenantId);
        if (tenant is null || !tenant.IsActive)
            return Results.Redirect("/login?error=tenant_inactive");

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: true);
        return result.Succeeded
            ? Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl)
            : Results.Redirect("/login?error=1");
    })
    .RequireRateLimiting("login");

app.MapPost("/account/logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
});

// Internal-only dashboard for monitoring background jobs (scheduled reports, leave accrual, etc.)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HRMS.Web.Infrastructure.HangfireAuthFilter() }
});

// Runs once daily, aggregating the previous day's checkins into AttendanceRecords for
// every active tenant (see AttendanceGenerationJob for why it iterates tenants explicitly).
RecurringJob.AddOrUpdate<HRMS.Infrastructure.Jobs.AttendanceGenerationJob>(
    "daily-attendance-generation",
    job => job.RunForAllTenantsAsync(),
    Cron.Daily(1, 0)); // 01:00 server time

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
