using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GaiaSkyline.Web.Health;

/// <summary>
/// Reports external-calendar sync health: "Manual mode" (Healthy) when nothing is configured, Degraded
/// when an enabled source is repeatedly failing, otherwise Healthy.
/// </summary>
public sealed class ExternalCalendarsHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var enabled = await dbContext.ExternalCalendarSources.CountAsync(s => s.IsEnabled, cancellationToken);
        if (enabled == 0)
        {
            return HealthCheckResult.Healthy("Manual mode — no external calendars configured.");
        }

        var failing = await dbContext.ExternalCalendarSources
            .CountAsync(s => s.IsEnabled && s.ConsecutiveFailures >= 3, cancellationToken);

        return failing > 0
            ? HealthCheckResult.Degraded($"{failing} of {enabled} external calendar source(s) are failing to sync.")
            : HealthCheckResult.Healthy($"{enabled} external calendar source(s) syncing.");
    }
}
