using System.Globalization;
using System.Text.Json.Serialization;
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

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can host the app in tests.</summary>
public partial class Program;
