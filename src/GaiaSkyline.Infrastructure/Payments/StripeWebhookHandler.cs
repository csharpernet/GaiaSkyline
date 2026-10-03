using GaiaSkyline.Application.Availability;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Payments;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;

namespace GaiaSkyline.Infrastructure.Payments;

/// <summary>
/// Verifies and processes Stripe webhooks — the source of truth for confirmation. Every event is
/// logged first (unique <c>StripeEventId</c>); a delivery already processed is a no-op, while a prior
/// failure is re-processed on Stripe's retry. A handler failure records the error and returns 500.
/// </summary>
internal sealed class StripeWebhookHandler(
    AppDbContext dbContext,
    IOptions<StripeOptions> options,
    IBookingLifecycleService lifecycle,
    IAvailabilityService availability,
    IIcsCacheInvalidator icsCache,
    IBookingNotificationService notifications,
    TimeProvider clock) : IStripeWebhookHandler
{
    // Stripe event-type strings (API contract; stable across SDK versions).
    private const string PaymentIntentSucceeded = "payment_intent.succeeded";
    private const string PaymentIntentProcessing = "payment_intent.processing";
    private const string PaymentIntentPaymentFailed = "payment_intent.payment_failed";
    private const string ChargeRefunded = "charge.refunded";
    private const string ChargeDisputeCreated = "charge.dispute.created";
    private const string ChargeDisputeClosed = "charge.dispute.closed";

    private readonly StripeOptions _options = options.Value;

    public async Task<WebhookResult> HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload, signatureHeader, _options.WebhookSecret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException)
        {
            return new WebhookResult(WebhookOutcome.InvalidSignature, "Signature verification failed.");
        }

        var log = await dbContext.StripeEventLogs
            .FirstOrDefaultAsync(e => e.StripeEventId == stripeEvent.Id, cancellationToken);
        if (log is not null)
        {
            if (log.ProcessedAtUtc is not null)
            {
                return new WebhookResult(WebhookOutcome.Ok, "Duplicate event ignored.");
            }
            // A previous attempt failed; fall through and re-process using the existing log row.
        }
        else
        {
            log = new StripeEventLog(
                StripeEventLogId.New(), stripeEvent.Id, stripeEvent.Type, payload, clock.GetUtcNow().UtcDateTime);
            dbContext.StripeEventLogs.Add(log);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent delivery inserted the same event first; treat as a duplicate.
                return new WebhookResult(WebhookOutcome.Ok, "Duplicate event ignored.");
            }
        }

        try
        {
            await DispatchAsync(stripeEvent, cancellationToken);
            log.MarkProcessed(clock.GetUtcNow().UtcDateTime);
            await dbContext.SaveChangesAsync(cancellationToken);
            return new WebhookResult(WebhookOutcome.Ok);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.MarkFailed(ex.Message);
            await dbContext.SaveChangesAsync(CancellationToken.None);
            return new WebhookResult(WebhookOutcome.Error, ex.Message);
        }
    }

    private async Task DispatchAsync(Event stripeEvent, CancellationToken cancellationToken)
    {
        switch (stripeEvent.Type)
        {
            case PaymentIntentSucceeded:
                await OnPaymentSucceededAsync((PaymentIntent)stripeEvent.Data.Object, cancellationToken);
                break;
            case PaymentIntentProcessing:
                await OnPaymentProcessingAsync((PaymentIntent)stripeEvent.Data.Object, cancellationToken);
                break;
            case PaymentIntentPaymentFailed:
                await OnPaymentFailedAsync((PaymentIntent)stripeEvent.Data.Object, cancellationToken);
                break;
            case ChargeRefunded:
                await OnChargeRefundedAsync((Charge)stripeEvent.Data.Object, cancellationToken);
                break;
            case ChargeDisputeCreated:
                await OnDisputeCreatedAsync((Dispute)stripeEvent.Data.Object, cancellationToken);
                break;
            case ChargeDisputeClosed:
                // Logged via the StripeEventLog row; no state change.
                break;
            default:
                // Unsubscribed event types are acknowledged and ignored.
                break;
        }
    }

    private async Task OnPaymentSucceededAsync(PaymentIntent intent, CancellationToken cancellationToken)
    {
        var booking = await FindByPaymentIntentAsync(intent.Id, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.AwaitingPayment)
        {
            return;
        }

        booking.ConfirmPayment(DeterminePaymentMethod(booking, intent), clock.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);

        availability.Invalidate();
        icsCache.Invalidate();
        await notifications.SendGuestConfirmationAsync(booking, cancellationToken);
        await notifications.SendOwnerNotificationAsync(booking, cancellationToken);
        await notifications.SendPropertyManagerNotificationAsync(booking, cancellationToken);
    }

    private async Task OnPaymentProcessingAsync(PaymentIntent intent, CancellationToken cancellationToken)
    {
        var booking = await FindByPaymentIntentAsync(intent.Id, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.AwaitingPayment)
        {
            return;
        }

        // Multibanco: persist the voucher (entity/reference/expiry) and keep holding the dates.
        var voucher = intent.NextAction?.MultibancoDisplayDetails;
        if (voucher is not null && booking.MultibancoReference is null
            && !string.IsNullOrEmpty(voucher.Entity) && !string.IsNullOrEmpty(voucher.Reference))
        {
            var expiresAtUtc = voucher.ExpiresAt?.ToUniversalTime()
                ?? clock.GetUtcNow().UtcDateTime.AddDays(_options.MultibancoMinLeadDays);
            booking.SetMultibancoVoucher(voucher.Entity, voucher.Reference, expiresAtUtc);
            await dbContext.SaveChangesAsync(cancellationToken);
            await notifications.SendMultibancoReferenceAsync(booking, cancellationToken);
        }
    }

    private async Task OnPaymentFailedAsync(PaymentIntent intent, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.StripePaymentIntentId == intent.Id, cancellationToken);
        if (booking is null || booking.Status != BookingStatus.AwaitingPayment)
        {
            return;
        }

        // Multibanco voucher expiry → release the dates. A card decline stays AwaitingPayment so the
        // guest can retry until the 30-minute hold expires.
        if (booking.MultibancoReference is not null)
        {
            await lifecycle.CancelAndReleaseAsync(booking.Id, "Multibanco voucher expired", cancellationToken);
            var cancelled = await dbContext.Bookings.AsNoTracking()
                .FirstAsync(b => b.Id == booking.Id, cancellationToken);
            await notifications.SendPaymentExpiredAsync(cancelled, cancellationToken);
        }
    }

    private async Task OnChargeRefundedAsync(Charge charge, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(charge.PaymentIntentId))
        {
            return;
        }

        var booking = await FindByPaymentIntentAsync(charge.PaymentIntentId, cancellationToken);
        if (booking is null)
        {
            return;
        }

        var fullyRefunded = charge.AmountRefunded >= charge.Amount;
        var target = fullyRefunded ? BookingStatus.Refunded : BookingStatus.PartiallyRefunded;
        if (!BookingStatusTransitions.CanTransition(booking.Status, target))
        {
            return;
        }

        if (fullyRefunded)
        {
            booking.MarkRefunded();
        }
        else
        {
            booking.MarkPartiallyRefunded();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await notifications.SendRefundAsync(booking, cancellationToken);
    }

    private async Task OnDisputeCreatedAsync(Dispute dispute, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(dispute.PaymentIntentId))
        {
            return;
        }

        var booking = await FindByPaymentIntentAsync(dispute.PaymentIntentId, cancellationToken);
        if (booking is not null)
        {
            await notifications.SendDisputeAlertAsync(booking, cancellationToken);
        }
    }

    private async Task<Booking?> FindByPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken) =>
        await dbContext.Bookings.FirstOrDefaultAsync(b => b.StripePaymentIntentId == paymentIntentId, cancellationToken);

    private static string DeterminePaymentMethod(Booking booking, PaymentIntent intent)
    {
        if (booking.MultibancoReference is not null)
        {
            return "multibanco";
        }

        return intent.PaymentMethodTypes is { Count: 1 } single ? single[0] : "card";
    }
}
