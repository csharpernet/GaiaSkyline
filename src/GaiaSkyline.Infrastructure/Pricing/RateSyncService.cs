using System.Globalization;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Pricing;

/// <summary>
/// Applies provider rates to the next 18 months: skips owner-locked dates, applies the direct-booking
/// adjustment (rounded to whole euros), rejects values outside the floor/ceiling (keeping the previous
/// value and alerting the owner), and upserts only changed dates. No provider configured → manual mode.
/// </summary>
internal sealed class RateSyncService(
    AppDbContext dbContext,
    IEnumerable<IRateProvider> providers,
    IOptions<PricingProviderOptions> options,
    IContentRevision revision,
    IAvailabilityService availabilityService,
    IEmailSender emailSender,
    IOptions<EmailOptions> emailOptions,
    TimeProvider clock,
    ILogger<RateSyncService> logger) : IRateSyncService
{
    public async Task SyncAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        var provider = providers.FirstOrDefault();
        if (config.Provider == RateProviderType.None || provider is null)
        {
            logger.LogInformation("Automatic pricing provider not configured — manual pricing mode.");
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var from = DateOnly.FromDateTime(now);
        var to = from.AddMonths(18);

        IReadOnlyList<ProviderRate> providerRates;
        try
        {
            providerRates = await provider.GetRatesAsync(from, to, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Rate sync from {Provider} failed.", config.Provider);
            await AlertSyncFailedAsync(ex.Message, cancellationToken);
            return;
        }

        var existing = (await dbContext.DailyRates
            .Where(d => d.Date >= from && d.Date < to)
            .ToListAsync(cancellationToken))
            .ToDictionary(d => d.Date);

        var source = config.Provider == RateProviderType.PriceLabs ? RateSource.PriceLabs : RateSource.Hostify;
        var rejected = new List<(DateOnly Date, decimal Price)>();
        var changed = false;

        // Open rejections are upserted per date so the review page (Stage 7 §8) never piles up rows.
        var openRejections = (await dbContext.RateSyncRejections
            .Where(r => r.Status == RateSyncRejectionStatus.Open)
            .ToListAsync(cancellationToken))
            .ToDictionary(r => r.Date);

        foreach (var rate in providerRates)
        {
            // Owner-locked dates are never overwritten by an import.
            if (existing.TryGetValue(rate.Date, out var current) && current.IsLockedByOwner)
            {
                continue;
            }

            // Direct-booking adjustment, rounded to whole euros.
            var adjusted = Math.Round(rate.Price * (1m + (config.DirectBookingAdjustmentPct / 100m)), 0, MidpointRounding.AwayFromZero);

            if (adjusted < config.FloorPrice || adjusted > config.CeilingPrice)
            {
                rejected.Add((rate.Date, adjusted));
                var reason = adjusted < config.FloorPrice
                    ? $"below floor €{config.FloorPrice:0}"
                    : $"above ceiling €{config.CeilingPrice:0}";
                if (openRejections.TryGetValue(rate.Date, out var openRejection))
                {
                    openRejection.UpdateOffer(adjusted, rate.MinNights, reason, now);
                }
                else
                {
                    dbContext.RateSyncRejections.Add(new RateSyncRejection(
                        RateSyncRejectionId.New(), rate.Date, adjusted, rate.MinNights,
                        config.Provider.ToString(), reason, now));
                }

                continue; // keep the previous value
            }

            var money = new Money(adjusted, "EUR");
            if (existing.TryGetValue(rate.Date, out var daily))
            {
                if (daily.NightlyRate.Amount != adjusted || daily.MinNights != rate.MinNights)
                {
                    daily.SetRate(money, rate.MinNights, source, now, "rate-sync", now);
                    changed = true;
                }
            }
            else
            {
                dbContext.DailyRates.Add(new DailyRate(rate.Date, money, rate.MinNights, source, now, "rate-sync", sourceUpdatedAtUtc: now));
                changed = true;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (changed)
        {
            revision.Bump();
            availabilityService.Invalidate();
        }

        if (rejected.Count > 0)
        {
            await AlertRejectedAsync(rejected, config, cancellationToken);
        }
    }

    private async Task AlertRejectedAsync(
        List<(DateOnly Date, decimal Price)> rejected, PricingProviderOptions config, CancellationToken cancellationToken)
    {
        var rows = string.Join(string.Empty, rejected.Select(r =>
            $"<li>{r.Date.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}: €{r.Price.ToString("0", CultureInfo.InvariantCulture)}</li>"));
        var body =
            $"<p><strong>Rate import rejected {rejected.Count} value(s)</strong> outside the configured bounds " +
            $"(€{config.FloorPrice:0}–€{config.CeilingPrice:0}); the previous rates were kept:</p><ul>{rows}</ul>";
        await emailSender.SendAsync(
            new EmailMessage(emailOptions.Value.OwnerAddress, emailOptions.Value.FromName, "Rate import: values out of bounds", body),
            cancellationToken);
    }

    private async Task AlertSyncFailedAsync(string reason, CancellationToken cancellationToken)
    {
        var body =
            "<p><strong>Automatic rate sync failed.</strong> Nightly prices were not updated; the last known " +
            $"rates are still in effect. Set prices manually if this persists.</p><p>Details: {reason}</p>";
        await emailSender.SendAsync(
            new EmailMessage(emailOptions.Value.OwnerAddress, emailOptions.Value.FromName, "Rate sync failed", body),
            cancellationToken);
    }
}
