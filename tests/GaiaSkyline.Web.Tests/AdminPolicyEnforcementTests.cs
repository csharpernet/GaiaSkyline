using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// The Owner policy's three authenticated gates on every /admin route (the anonymous gate lives in
/// <see cref="AdminAuthorizationTests"/>): a wrong role is forbidden, an Owner cookie without a completed
/// two-factor sign-in (no amr=mfa claim — e.g. the pre-enrolment password-only window) is forbidden, and an
/// Owner outside the configured IP allowlist is forbidden; the full combination passes. Stage 7 Tests list.
/// </summary>
public sealed class AdminPolicyEnforcementTests(AdminPolicyFactory factory) : IClassFixture<AdminPolicyFactory>
{
    public const string AllowedIp = "203.0.113.7";

    [Theory]
    [MemberData(nameof(AdminAuthorizationTests.AdminRoutes), MemberType = typeof(AdminAuthorizationTests))]
    public async Task A_wrong_role_is_forbidden(string route) =>
        (await GetAsync(route, role: "Guest", mfa: true, ip: AllowedIp))
            .Should().Be(HttpStatusCode.Forbidden, $"{route} must reject a non-Owner role");

    [Theory]
    [MemberData(nameof(AdminAuthorizationTests.AdminRoutes), MemberType = typeof(AdminAuthorizationTests))]
    public async Task An_owner_without_completed_two_factor_is_forbidden(string route) =>
        (await GetAsync(route, role: "Owner", mfa: false, ip: AllowedIp))
            .Should().Be(HttpStatusCode.Forbidden, $"{route} must reject a password-only Owner session");

    [Theory]
    [MemberData(nameof(AdminAuthorizationTests.AdminRoutes), MemberType = typeof(AdminAuthorizationTests))]
    public async Task An_owner_outside_the_ip_allowlist_is_forbidden(string route) =>
        (await GetAsync(route, role: "Owner", mfa: true, ip: "198.51.100.9"))
            .Should().Be(HttpStatusCode.Forbidden, $"{route} must reject an IP outside Owner:AllowedIps");

    [Theory]
    [MemberData(nameof(AdminAuthorizationTests.AdminRoutes), MemberType = typeof(AdminAuthorizationTests))]
    public async Task An_owner_with_two_factor_from_an_allowed_ip_is_let_through(string route) =>
        (await GetAsync(route, role: "Owner", mfa: true, ip: AllowedIp))
            .Should().Be(HttpStatusCode.OK, $"{route} must serve the fully-authenticated Owner");

    private async Task<HttpStatusCode> GetAsync(string route, string role, bool mfa, string ip)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));
        request.Headers.Add(ClaimsStubHandler.RoleHeader, role);
        if (mfa)
        {
            request.Headers.Add(ClaimsStubHandler.MfaHeader, "mfa");
        }

        request.Headers.Add(AdminPolicyFactory.IpHeader, ip);
        var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}

/// <summary>
/// Boots the real app (migrated + seeded throwaway LocalDB, like <see cref="PublicSiteFactory"/>) with an
/// Owner IP allowlist configured and a header-driven claims stub as the default authenticate/forbid scheme,
/// so each request can impersonate a principal (role/amr) and a source IP without driving the login UI. The
/// real authorization middleware and Owner policy run unchanged — they are what is under test.
/// </summary>
public sealed class AdminPolicyFactory : WebApplicationFactory<Program>
{
    public const string IpHeader = "X-Test-Ip";

    private readonly string _databaseName = $"GaiaSkyline_Web_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $@"Server=(localdb)\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=60";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Features:SeedContentOnStartup"] = "true",
                ["BackgroundJobs:Enabled"] = "false",
                ["Owner:AllowedIps:0"] = AdminPolicyEnforcementTests.AllowedIp,
            }));
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, ClaimsStubHandler>(ClaimsStubHandler.SchemeName, _ => { });
            services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = ClaimsStubHandler.SchemeName;
                options.DefaultChallengeScheme = ClaimsStubHandler.SchemeName;
                options.DefaultForbidScheme = ClaimsStubHandler.SchemeName;
            });
            // TestServer has no socket, so Connection.RemoteIpAddress is null; stamp it from a test header
            // before anything else runs so the IP-allowlist handler sees a realistic source address.
            services.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter>(new RemoteIpStartupFilter());
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var scope = Services.CreateScope();
                scope.ServiceProvider.GetService<AppDbContext>()?.Database.EnsureDeleted();
            }
            catch
            {
                // Best effort — the throwaway database is uniquely named.
            }
        }

        base.Dispose(disposing);
    }

    private sealed class RemoteIpStartupFilter : Microsoft.AspNetCore.Hosting.IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(
            Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            app.Use(nextMiddleware => context =>
            {
                if (context.Request.Headers.TryGetValue(IpHeader, out var value)
                    && IPAddress.TryParse(value.ToString(), out var parsed))
                {
                    context.Connection.RemoteIpAddress = parsed;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

/// <summary>Authenticates from test headers: a role claim, optionally amr=mfa. Forbid renders 403.</summary>
public sealed class ClaimsStubHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestClaims";
    public const string RoleHeader = "X-Test-Role";
    public const string MfaHeader = "X-Test-Amr";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var role) || string.IsNullOrWhiteSpace(role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user"),
            new(ClaimTypes.Name, "test-user@gaiaskyline.test"),
            new(ClaimTypes.Role, role.ToString()),
        };
        if (Request.Headers.TryGetValue(MfaHeader, out var amr) && !string.IsNullOrWhiteSpace(amr))
        {
            claims.Add(new Claim("amr", amr.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Task.CompletedTask;
    }
}
