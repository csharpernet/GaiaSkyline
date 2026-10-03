using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Web.Health;

/// <summary>
/// Reports automatic-pricing health: "Manual pricing mode" (Healthy) when no provider is configured,
/// Degraded when a provider is configured but no rate has synced in the last 24 hours, otherwise Healthy.
/// </summary>
public sealed class PricingHealthCheck(
    AppDbContext dbContext,
    IOptions<PricingProviderOptions> options,
    TimeProvider clock) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (options.Value.Provider == RateProviderType.None)
        {
            return HealthCheckResult.Healthy("Manual pricing mode — no automatic provider configured.");
        }

        var lastSync = await dbContext.DailyRates
            .Where(d => d.Source != RateSource.Manual && d.SourceUpdatedAtUtc != null)
            .MaxAsync(d => (DateTime?)d.SourceUpdatedAtUtc, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        if (lastSync is null || now - lastSync.Value > TimeSpan.FromHours(24))
        {
            return HealthCheckResult.Degraded(
                $"Automatic pricing provider '{options.Value.Provider}' has not synced in the last 24 hours.");
        }

        return HealthCheckResult.Healthy($"Automatic pricing provider '{options.Value.Provider}' synced at {lastSync:u}.");
    }
}
