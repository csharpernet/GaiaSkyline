using System.Globalization;
using System.Text.Json.Serialization;
using FluentValidation;
using GaiaSkyline.Application;
using GaiaSkyline.BackgroundJobs;
using GaiaSkyline.Infrastructure;
using GaiaSkyline.Infrastructure.Data;
using GaiaSkyline.Infrastructure.Persistence;
using GaiaSkyline.Web.Localization;
using GaiaSkyline.Web.Middleware;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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

// QuestPDF Community licence (free for orgs under the revenue threshold); see ADR 0012.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Per-IP rate limits on the booking endpoints (abuse protection; checkout is the stricter one).
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

var app = builder.Build();

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
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

// All controllers are attribute-routed: the public site (/{lang}/...), the APIs (/api/...),
// the root redirect, sitemap/robots and the error page.
app.MapControllers();

// Hangfire is wired via AddBackgroundJobs. The dashboard is intentionally NOT mapped yet — it
// needs authentication, which arrives with the admin area in a later stage.
// Unpaid-hold expiry runs every 5 minutes as a safety net alongside webhook handling. Use the
// DI-based IRecurringJobManager (not the static RecurringJob) so it binds to the configured storage.
if (GaiaSkyline.BackgroundJobs.DependencyInjection.IsEnabled(builder.Configuration))
{
    using var jobsScope = app.Services.CreateScope();
    var recurringJobs = jobsScope.ServiceProvider.GetRequiredService<Hangfire.IRecurringJobManager>();
    Hangfire.RecurringJobManagerExtensions.AddOrUpdate<GaiaSkyline.Application.Bookings.IBookingExpiryService>(
        recurringJobs,
        "unpaid-hold-expiry",
        service => service.ExpireUnpaidHoldsAsync(CancellationToken.None),
        "*/5 * * * *");
}

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the app in tests.</summary>
public partial class Program;
