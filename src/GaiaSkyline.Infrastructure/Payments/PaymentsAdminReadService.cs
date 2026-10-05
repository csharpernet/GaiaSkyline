using System.Text.Json;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace GaiaSkyline.Infrastructure.Payments;

/// <summary>
/// Payments reads for the admin (Stage 7 §9). The Stripe account status and open disputes come live
/// from the Stripe API (gracefully "not configured" without a key); the event browser and refund
/// history read the local StripeEventLog (refund amounts parsed from the stored payloads); the
/// Multibanco monitor lists AwaitingPayment holds with a voucher.
/// </summary>
internal sealed class PaymentsAdminReadService(
    AppDbContext dbContext,
    Lazy<IStripeClient> stripeClient,
    IOptions<StripeOptions> stripeOptions,
    ILogger<PaymentsAdminReadService> logger) : IPaymentsAdminReadService
{
    private const string ChargeRefunded = "charge.refunded";

    public async Task<StripeStatusDto> GetStripeStatusAsync(CancellationToken cancellationToken)
    {
        var lastWebhook = await dbContext.StripeEventLogs.AsNoTracking()
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Select(e => (DateTime?)e.ReceivedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var secretKey = stripeOptions.Value.SecretKey;
        if (string.IsNullOrEmpty(secretKey))
        {
            return new StripeStatusDto(
                Configured: false, LiveMode: false,
                "Stripe is not configured — set Stripe:SecretKey (User Secrets in dev).", [], lastWebhook);
        }

        var liveMode = secretKey.Contains("_live_", StringComparison.Ordinal);
        try
        {
            var account = await new AccountService(stripeClient.Value).GetSelfAsync(null, cancellationToken);
            var name = account.Settings?.Dashboard?.DisplayName ?? account.Email ?? account.Id;
            var summary = $"{name} · charges {(account.ChargesEnabled ? "enabled" : "DISABLED")} · payouts {(account.PayoutsEnabled ? "enabled" : "DISABLED")}";
            var methods = new List<string>();
            if (account.Capabilities?.CardPayments is { } card)
            {
                methods.Add($"card: {card}");
            }

            if (account.Capabilities?.MultibancoPayments is { } multibanco)
            {
                methods.Add($"multibanco: {multibanco}");
            }

            return new StripeStatusDto(true, liveMode, summary, methods, lastWebhook);
        }
        catch (Exception ex) when (ex is StripeException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Stripe account status read failed.");
            return new StripeStatusDto(true, liveMode, $"Stripe API unreachable: {ex.Message}", [], lastWebhook);
        }
    }

    public async Task<StripeEventPageDto> GetEventsAsync(StripeEventFilter filter, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var query = dbContext.StripeEventLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            query = query.Where(e => e.Type == filter.Type);
        }

        if (filter.OnlyFailed)
        {
            query = query.Where(e => e.ProcessedAtUtc == null);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new StripeEventRowDto(e.Id.Value, e.StripeEventId, e.Type, e.ReceivedAtUtc, e.ProcessedAtUtc, e.Error))
            .ToListAsync(cancellationToken);
        var types = await dbContext.StripeEventLogs.AsNoTracking()
            .Select(e => e.Type).Distinct().OrderBy(t => t).Take(100).ToListAsync(cancellationToken);

        return new StripeEventPageDto(items, total, page, pageSize, types);
    }

    public async Task<IReadOnlyList<RefundRowDto>> GetRefundHistoryAsync(CancellationToken cancellationToken)
    {
        var events = await dbContext.StripeEventLogs.AsNoTracking()
            .Where(e => e.Type == ChargeRefunded)
            .OrderByDescending(e => e.ReceivedAtUtc)
            .Take(100)
            .Select(e => new { e.ReceivedAtUtc, e.PayloadJson })
            .ToListAsync(cancellationToken);
        if (events.Count == 0)
        {
            return [];
        }

        var parsed = new List<(DateTime ReceivedAtUtc, string PaymentIntentId, decimal AmountEur, bool Full)>();
        foreach (var row in events)
        {
            try
            {
                using var doc = JsonDocument.Parse(row.PayloadJson);
                var charge = doc.RootElement.GetProperty("data").GetProperty("object");
                var intent = charge.TryGetProperty("payment_intent", out var pi) ? pi.GetString() : null;
                if (string.IsNullOrEmpty(intent))
                {
                    continue;
                }

                var amount = charge.TryGetProperty("amount", out var a) ? a.GetInt64() : 0L;
                var refunded = charge.TryGetProperty("amount_refunded", out var r) ? r.GetInt64() : 0L;
                parsed.Add((row.ReceivedAtUtc, intent, refunded / 100m, refunded >= amount && amount > 0));
            }
            catch (JsonException)
            {
                // A malformed stored payload never breaks the page.
            }
        }

        var intents = parsed.Select(p => p.PaymentIntentId).Distinct().ToList();
        var bookings = await dbContext.Bookings.AsNoTracking()
            .Where(b => b.StripePaymentIntentId != null && intents.Contains(b.StripePaymentIntentId))
            .Select(b => new { b.StripePaymentIntentId, b.ReferenceCode, b.Id })
            .ToListAsync(cancellationToken);
        var byIntent = bookings.ToDictionary(b => b.StripePaymentIntentId!, b => (b.ReferenceCode, b.Id.Value));

        return parsed
            .Select(p => new RefundRowDto(
                p.ReceivedAtUtc, p.PaymentIntentId,
                byIntent.TryGetValue(p.PaymentIntentId, out var booking) ? booking.ReferenceCode : null,
                byIntent.TryGetValue(p.PaymentIntentId, out var b2) ? b2.Item2 : null,
                p.AmountEur, p.Full))
            .ToList();
    }

    public async Task<IReadOnlyList<DisputeRowDto>> GetOpenDisputesAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(stripeOptions.Value.SecretKey))
        {
            return [];
        }

        var liveMode = stripeOptions.Value.SecretKey.Contains("_live_", StringComparison.Ordinal);
        StripeList<Dispute> disputes;
        try
        {
            disputes = await new DisputeService(stripeClient.Value)
                .ListAsync(new DisputeListOptions { Limit = 50 }, null, cancellationToken);
        }
        catch (Exception ex) when (ex is StripeException or HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Stripe disputes read failed.");
            return [];
        }

        string[] openStatuses = ["needs_response", "under_review", "warning_needs_response", "warning_under_review"];
        var open = disputes.Data.Where(d => openStatuses.Contains(d.Status)).ToList();
        if (open.Count == 0)
        {
            return [];
        }

        var intents = open.Select(d => d.PaymentIntentId).Where(i => !string.IsNullOrEmpty(i)).Distinct().ToList();
        var bookings = await dbContext.Bookings.AsNoTracking()
            .Where(b => b.StripePaymentIntentId != null && intents.Contains(b.StripePaymentIntentId))
            .Select(b => new { b.StripePaymentIntentId, b.ReferenceCode, b.Id })
            .ToListAsync(cancellationToken);
        var byIntent = bookings.ToDictionary(b => b.StripePaymentIntentId!, b => (b.ReferenceCode, b.Id.Value));

        return open.Select(d =>
        {
            var hasBooking = d.PaymentIntentId is not null && byIntent.TryGetValue(d.PaymentIntentId, out var booking);
            var match = hasBooking ? byIntent[d.PaymentIntentId!] : default;
            return new DisputeRowDto(
                d.Id, d.PaymentIntentId,
                hasBooking ? match.ReferenceCode : null,
                hasBooking ? match.Item2 : null,
                d.Amount / 100m, d.Reason, d.Status,
                d.EvidenceDetails?.DueBy?.ToUniversalTime(),
                liveMode
                    ? $"https://dashboard.stripe.com/disputes/{d.Id}"
                    : $"https://dashboard.stripe.com/test/disputes/{d.Id}");
        }).ToList();
    }

    public async Task<IReadOnlyList<MultibancoRowDto>> GetMultibancoPendingAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.AwaitingPayment && b.MultibancoReference != null)
            .OrderBy(b => b.PaymentExpiresAtUtc)
            .Select(b => new { b.Id, b.ReferenceCode, b.Total, b.MultibancoEntity, b.MultibancoReference, b.PaymentExpiresAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .Select(b => new MultibancoRowDto(
                b.Id.Value, b.ReferenceCode, b.Total.Amount, b.MultibancoEntity, b.MultibancoReference, b.PaymentExpiresAtUtc))
            .ToList();
    }
}
