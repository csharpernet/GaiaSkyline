using System.Globalization;
using System.Text.Json.Serialization;
using FluentValidation;
using GaiaSkyline.Application;
using GaiaSkyline.BackgroundJobs;
using GaiaSkyline.Domain.Identity;
using GaiaSkyline.Infrastructure;
using GaiaSkyline.Infrastructure.Data;
using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Infrastructure.Persistence;
using GaiaSkyline.Web.Identity;
using GaiaSkyline.Web.Localization;
using GaiaSkyline.Web.Middleware;
using GaiaSkyline.Web.Security;
using Hangfire;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Logging: Serilog to console (App Insights sink is a placeholder wired in a later stage;
//     set ApplicationInsights:ConnectionString and add Serilog.Sinks.ApplicationInsights). ---
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

// --- MVC + application layers ---
builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // API payloads: enums as names, omit null fields.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddBackgroundJobs(builder.Configuration);
builder.Services.AddScoped<GaiaSkyline.Application.Storage.IMediaStorage, GaiaSkyline.Web.Storage.LocalDiskMediaStorage>();
builder.Services.AddSingleton<GaiaSkyline.Application.Media.IMediaDirectoryProvider, GaiaSkyline.Web.Storage.LocalMediaDirectoryProvider>();

// Owner content preview (Stage 7D): the signed cookie makes the public read render drafts.
builder.Services.AddScoped<GaiaSkyline.Web.Content.ContentPreview>();
builder.Services.AddScoped<GaiaSkyline.Application.Content.IContentPreviewState>(
    sp => sp.GetRequiredService<GaiaSkyline.Web.Content.ContentPreview>());

// --- E2E test seam (OFF by default; never enabled in Production — guarded below). The sink and options
//     are always registered so the test controller resolves and simply 404s when the seam is off; only
//     the capturing email sender replaces the real one when the seam is on. ---
builder.Services.AddOptions<GaiaSkyline.Web.Testing.E2EOptions>()
    .Bind(builder.Configuration.GetSection(GaiaSkyline.Web.Testing.E2EOptions.SectionName));
builder.Services.AddSingleton<GaiaSkyline.Web.Testing.E2EEmailSink>();
var e2eEnabled = builder.Configuration.GetValue<bool>(
    $"{GaiaSkyline.Web.Testing.E2EOptions.SectionName}:Enabled");
if (e2eEnabled)
{
    if (builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("The E2E test seam (E2E:Enabled) must never be enabled in Production.");
    }

    builder.Services.RemoveAll<GaiaSkyline.Application.Notifications.IEmailSender>();
    builder.Services.AddScoped<GaiaSkyline.Application.Notifications.IEmailSender, GaiaSkyline.Web.Testing.CapturingEmailSender>();
}

// --- Identity, authentication & authorization (Stage 6) ---
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        // NIST-style: length is the primary gate (12+), with the HIBP breach check (added below).
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredUniqueChars = 1;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;

        // Email confirmation is enforced per-role at sign-in (Partner must be confirmed), not globally.
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddGaiaIdentityStores();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAuthorizationHandler, OwnerIpAllowlistHandler>();
builder.Services.AddScoped<BookingAccessCookie>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "GaiaSkyline.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // Guest/Partner: sliding 14 days. The Owner is pinned to an 8-hour absolute session in OnSigningIn.
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/en/account/login";
    options.LogoutPath = "/en/account/logout";
    options.AccessDeniedPath = "/en/account/denied";
    options.Events = new CookieAuthenticationEvents
    {
        OnSigningIn = context =>
        {
            if (context.Principal?.IsInRole(UserRoles.Owner) == true)
            {
                context.Properties.IsPersistent = false;
                context.Properties.AllowRefresh = false;
                context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8);
            }

            return Task.CompletedTask;
        },
        // API surfaces answer with status codes, never a login redirect.
        OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        },
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.Owner, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(UserRoles.Owner);
        policy.Requirements.Add(new OwnerIpAllowlistRequirement());
    });
    options.AddPolicy(AuthorizationPolicies.Partner, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(UserRoles.Partner);
    });
    options.AddPolicy(AuthorizationPolicies.Guest, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireRole(UserRoles.Guest);
    });
});

// Partner JWT (Stage 6D). The signing key comes from User Secrets / Key Vault; when it is unset the app
// still boots with an ephemeral per-process key (tokens then don't survive a restart or scale-out — fine
// for dev/CI, and production is expected to configure Jwt:SigningKey).
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    jwtOptions.SigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
}

builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(jwtOptions));
builder.Services.AddSingleton<JwtTokenFactory>();
builder.Services.AddAuthentication().AddJwtBearer(AuthSchemes.PartnerJwt, options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true,
        ValidAudience = jwtOptions.Audience,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        ClockSkew = TimeSpan.FromSeconds(30),
    };
});

// ICS export token (Stage 5). Stable value from config so external platforms can subscribe; an
// ephemeral per-process token is used when unset (fine in manual mode, where no one subscribes yet).
// Resolved lazily so the configured value is read after all config sources are composed.
var ephemeralIcsToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
builder.Services.AddSingleton(sp =>
{
    var configured = sp.GetRequiredService<IConfiguration>()["Ics:ExportToken"];
    return new IcsExportOptions { Token = string.IsNullOrWhiteSpace(configured) ? ephemeralIcsToken : configured };
});

// QuestPDF Community licence (free for orgs under the revenue threshold); see ADR 0012.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Per-IP rate limits on the booking endpoints (abuse protection; checkout is the stricter one).
// Under the E2E seam the whole Playwright suite signs the one Owner in many times from a single IP
// (each login hits /admin/login + /admin/two-factor), which would exhaust the production login budget;
// relax the auth limits there only. The seam is dev/CI-only and throws if ever enabled in Production.
var loginPermitLimit = e2eEnabled ? 10_000 : 10;
var authPermitLimit = e2eEnabled ? 10_000 : 5;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("quote", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
        }));
    options.AddPolicy("checkout", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10),
        }));
    // Auth abuse protection (Stage 6): login/register and the token-sending endpoints.
    options.AddPolicy("login", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = loginPermitLimit,
            Window = TimeSpan.FromMinutes(15),
        }));
    options.AddPolicy("auth", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = authPermitLimit,
            Window = TimeSpan.FromMinutes(15),
        }));
});
builder.Services.AddValidatorsFromAssemblyContaining<GaiaSkyline.Web.Api.CheckoutRequest>();
// Antiforgery token sent as a header by the checkout fetch() (JSON POST, not a form post).
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

// --- Output caching: public pages cached 10 min, keyed by {lang} + the content revision
//     (a revision bump changes the key, so published content invalidates the cache). ---
builder.Services.AddOutputCache(options =>
    options.AddPolicy("public", policy => policy
        .Expire(TimeSpan.FromMinutes(10))
        .SetVaryByRouteValue("lang")
        // Bypass the cache entirely during an owner preview so drafts render live.
        .With(context => !context.HttpContext.Request.Cookies.ContainsKey(GaiaSkyline.Web.Content.ContentPreview.CookieName))
        .VaryByValue(static (context, _) =>
        {
            var revision = context.RequestServices
                .GetRequiredService<GaiaSkyline.Application.Content.IContentRevision>().Current;
            return ValueTask.FromResult(
                new KeyValuePair<string, string>("rev", revision.ToString(CultureInfo.InvariantCulture)));
        })));

// --- Localization: URL-based (/{lang}/...). Resolve route -> ?lang (API) -> cookie -> Accept-Language -> en ---
builder.Services.Configure<RouteOptions>(options =>
    options.ConstraintMap["culture"] = typeof(CultureRouteConstraint));

var supportedCultures = SupportedCultures.AllCultures.ToArray();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture(SupportedCultures.DefaultCulture);
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
    options.ApplyCurrentCultureToResponseHeaders = true;
    options.RequestCultureProviders =
    [
        new RouteCultureProvider(),
        new QueryStringRequestCultureProvider { QueryStringKey = "lang", UIQueryStringKey = "lang" },
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider(),
    ];
});

// --- Antiforgery + cookie policy (SameSite=Lax; Secure in non-dev) ---
var secureCookiePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "GaiaSkyline.Antiforgery";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = secureCookiePolicy;
    options.Cookie.HttpOnly = true;
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.HttpOnly = HttpOnlyPolicy.Always;
    options.Secure = secureCookiePolicy;
});

// --- Health checks: /health/live (process) and /health/ready (dependencies) ---
var healthChecks = builder.Services.AddHealthChecks();
var connectionString =
    builder.Configuration[$"ConnectionStrings:{GaiaSkyline.Infrastructure.DependencyInjection.ConnectionStringName}"];
if (!string.IsNullOrWhiteSpace(connectionString))
{
    healthChecks.AddSqlServer(connectionString, name: "sql-server", tags: ["ready"]);
}

healthChecks.AddTypeActivatedCheck<GaiaSkyline.Web.Health.ExternalCalendarsHealthCheck>(
    "external-calendars", failureStatus: null, tags: ["ready"]);

healthChecks.AddTypeActivatedCheck<GaiaSkyline.Web.Health.PricingHealthCheck>(
    "pricing-sync", failureStatus: null, tags: ["ready"]);

var app = builder.Build();

// One-off CLI: `dotnet run -- create-owner --email ... --password ...` (prod Owner provisioning).
if (args.Length > 0 && string.Equals(args[0], OwnerCli.CommandName, StringComparison.OrdinalIgnoreCase))
{
    return await OwnerCli.RunAsync(app, args);
}

// Development convenience: apply migrations and seed canonical content on startup. Gated by a
// flag so the test host (and any environment that opts out) never touches the database here.
var seedOnStartup = bool.TryParse(app.Configuration["Features:SeedContentOnStartup"], out var seedFlag) && seedFlag;
if (app.Environment.IsDevelopment() && seedOnStartup)
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await database.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<ContentSeeder>();
    var webRoot = app.Environment.WebRootPath;
    var mediaRoot = string.IsNullOrEmpty(webRoot) ? null : Path.Combine(webRoot, "media");
    await seeder.SeedAsync(mediaRoot, CancellationToken.None);

    var bookingSeeder = scope.ServiceProvider.GetRequiredService<BookingSeeder>();
    await bookingSeeder.SeedAsync(CancellationToken.None);

    // External calendar sources from config (ExternalCalendars:Sources); empty by default = manual mode.
    var calendarSeeder = scope.ServiceProvider
        .GetRequiredService<GaiaSkyline.Infrastructure.Availability.ExternalCalendarSourceSeeder>();
    await calendarSeeder.SeedAsync(CancellationToken.None);

    // Roles always exist in Development; the Owner is seeded when Owner:Email + Owner:Password are set
    // (put them in User Secrets). There is no self-registration into the Owner role.
    var identitySeeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
    await identitySeeder.EnsureRolesAsync();
    var ownerEmail = app.Configuration["Owner:Email"];
    var ownerPassword = app.Configuration["Owner:Password"];
    if (!string.IsNullOrWhiteSpace(ownerEmail) && !string.IsNullOrWhiteSpace(ownerPassword))
    {
        await identitySeeder.EnsureOwnerAsync(ownerEmail, ownerPassword);
    }

    // Deterministic fixtures for the Playwright E2E suite (Owner 2FA key + a known booking).
    if (e2eEnabled)
    {
        await GaiaSkyline.Web.Testing.E2ESeeder.SeedAsync(scope.ServiceProvider, CancellationToken.None);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseStaticFiles();

// After static files so it only handles misses: 301 an old (renamed) /media filename to its current URL.
app.UseMiddleware<MediaAliasRedirectMiddleware>();

// Canonicalise to no trailing slash (301), except the root "/".
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path is { Length: > 1 } && path.EndsWith('/'))
    {
        context.Response.Redirect(path.TrimEnd('/') + context.Request.QueryString, permanent: true);
        return;
    }

    await next(context);
});

app.UseRouting();
app.UseRequestLocalization();
app.UseOutputCache();
app.UseRateLimiter();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

// All controllers are attribute-routed: the public site (/{lang}/...), the APIs (/api/...),
// the root redirect, sitemap/robots and the error page.
app.MapControllers();

// Hangfire (when enabled): the dashboard is Owner-only (Stage 5 item 5), and the unpaid-hold expiry
// runs every 5 minutes as a safety net. Use the DI-based IRecurringJobManager (not the static
// RecurringJob) so it binds to the configured storage.
if (GaiaSkyline.BackgroundJobs.DependencyInjection.IsEnabled(builder.Configuration))
{
    app.UseHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
    {
        Authorization = [new OwnerDashboardAuthorizationFilter()],
    });

    using var jobsScope = app.Services.CreateScope();
    var recurringJobs = jobsScope.ServiceProvider.GetRequiredService<Hangfire.IRecurringJobManager>();
    Hangfire.RecurringJobManagerExtensions.AddOrUpdate<GaiaSkyline.Application.Bookings.IBookingExpiryService>(
        recurringJobs,
        "unpaid-hold-expiry",
        service => service.ExpireUnpaidHoldsAsync(CancellationToken.None),
        "*/5 * * * *");

    // External calendar import (manual mode until a source is configured): every 15 minutes.
    var icsIntervalMinutes = int.TryParse(builder.Configuration["IcsImport:IntervalMinutes"], out var m) ? m : 15;
    Hangfire.RecurringJobManagerExtensions.AddOrUpdate<GaiaSkyline.Application.Availability.IExternalCalendarImporter>(
        recurringJobs,
        "external-calendar-import",
        importer => importer.ImportAllAsync(CancellationToken.None),
        $"*/{icsIntervalMinutes} * * * *");

    // Automatic nightly-price sync (manual mode until a provider is configured): every N hours.
    var rateSyncHours = int.TryParse(builder.Configuration[$"{GaiaSkyline.Application.Pricing.PricingProviderOptions.SectionName}:SyncIntervalHours"], out var h) && h > 0 ? h : 4;
    Hangfire.RecurringJobManagerExtensions.AddOrUpdate<GaiaSkyline.Application.Pricing.IRateSyncService>(
        recurringJobs,
        "rate-sync",
        service => service.SyncAsync(CancellationToken.None),
        $"0 */{rateSyncHours} * * *");

    // Daily 09:00 Europe/Lisbon nudge for direct bookings still to be mirrored in Hostify (manual mode);
    // the job no-ops when nothing is overdue (Stage 7C).
    Hangfire.RecurringJobManagerExtensions.AddOrUpdate<GaiaSkyline.Application.Admin.IManualSyncReminderService>(
        recurringJobs,
        "manual-sync-reminder",
        service => service.SendDueRemindersAsync(CancellationToken.None),
        "0 9 * * *",
        new Hangfire.RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Lisbon") });
}

app.Run();
return 0;

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the app in tests.</summary>
public partial class Program;
