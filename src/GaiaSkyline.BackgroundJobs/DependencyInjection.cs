using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.BackgroundJobs;

/// <summary>
/// Composition-root entry point for background processing (Hangfire on SQL Server).
/// Wired up but dormant: registration is gated behind <c>BackgroundJobs:Enabled</c> so the
/// app still boots in environments without a database. No recurring jobs exist yet.
/// </summary>
public static class DependencyInjection
{
    public const string EnabledConfigKey = "BackgroundJobs:Enabled";
    public const string ConnectionStringName = "Default";

    /// <summary>Whether background processing is switched on for this environment.</summary>
    public static bool IsEnabled(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return bool.TryParse(configuration[EnabledConfigKey], out var enabled) && enabled;
    }

    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!IsEnabled(configuration))
        {
            // Dormant: no Hangfire storage/server so the site runs without SQL Server.
            return services;
        }

        var connectionString = configuration[$"ConnectionStrings:{ConnectionStringName}"]
            ?? throw new InvalidOperationException(
                $"Background jobs are enabled but connection string '{ConnectionStringName}' is missing.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
            {
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.FromSeconds(15),
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                UseRecommendedIsolationLevel = true,
            }));

        services.AddHangfireServer();

        // Dispatch booking emails via Hangfire (overrides the inline fallback from AddInfrastructure).
        services.AddScoped<GaiaSkyline.Application.Notifications.IEmailJobScheduler, HangfireEmailJobScheduler>();

        return services;
    }
}
