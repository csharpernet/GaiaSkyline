using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>
/// Owner booking actions for the admin manager (Stage 7 §6). Transitions go through the domain status
/// machine; an illegal move is returned as a failed <see cref="BookingActionResult"/> rather than thrown.
/// Cancellation delegates to <see cref="IBookingLifecycleService"/> so the held nights are released, and
/// manual (phone/walk-in) bookings reuse <see cref="IBookingCreationService"/> so the same Serializable
/// hold + occupancy-key double-booking guards apply.
/// </summary>
internal sealed class AdminBookingService(
    AppDbContext dbContext,
    IBookingLifecycleService lifecycle,
    IBookingCreationService creation,
    IQuoteService quotes,
    IRefundService refunds,
    IEmailJobScheduler emails,
    GaiaSkyline.Application.Partners.IPartnerAttributionService partnerAttribution,
    TimeProvider clock) : IAdminBookingService
{
    public Task<BookingActionResult> CheckInAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, b => b.CheckInGuest(), "check in", cancellationToken);

    public Task<BookingActionResult> CompleteAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, b => b.Complete(), "complete", cancellationToken);

    public async Task<BookingActionResult> RefundAsync(Guid id, bool partial, decimal? amountEur, CancellationToken cancellationToken)
    {
        var booking = await FindAsync(id, cancellationToken);
        if (booking is null)
        {
            return BookingActionResult.Fail("Booking not found.");
        }

        var target = partial ? BookingStatus.PartiallyRefunded : BookingStatus.Refunded;
        if (!BookingStatusTransitions.CanTransition(booking.Status, target))
        {
            return BookingActionResult.Fail($"Cannot {(partial ? "partially refund" : "refund")} a booking that is {booking.Status}.");
        }

        if (amountEur is { } amount && (amount <= 0 || amount > booking.Total.Amount))
        {
            return BookingActionResult.Fail($"The refund must be between €0.01 and the booking total (€{booking.Total.Amount:0.00}).");
        }

        // With a Stripe payment the money moves through Stripe (the charge.refunded webhook would mark the
        // status too, but it is marked here as well so the admin sees the state immediately — the webhook's
        // transition check makes the double-mark a no-op). Without one it is a bookkeeping mark only.
        if (!string.IsNullOrEmpty(booking.StripePaymentIntentId))
        {
            await refunds.RefundAsync(booking.Id, partial ? amountEur : null, "owner_refund", cancellationToken);
        }

        // Keep the refunded total on the booking — it shrinks the partner-commission basis (ADR 0020).
        var refunded = partial
            ? new Money(amountEur ?? 0m, booking.Total.Currency)
            : booking.Total - booking.RefundedAmount;
        if (refunded.Amount > 0)
        {
            booking.RecordRefund(refunded);
        }

        if (partial)
        {
            booking.MarkPartiallyRefunded();
        }
        else
        {
            booking.MarkRefunded();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return BookingActionResult.Success;
    }

    public async Task<BookingActionResult> CancelAsync(Guid id, string? reason, decimal refundAmountEur, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == BookingId.From(id), cancellationToken);
        if (booking is null)
        {
            return BookingActionResult.Fail("Booking not found.");
        }

        if (refundAmountEur < 0 || refundAmountEur > booking.Total.Amount)
        {
            return BookingActionResult.Fail($"The refund must be between €0 and the booking total (€{booking.Total.Amount:0.00}).");
        }

        if (refundAmountEur > 0 && string.IsNullOrEmpty(booking.StripePaymentIntentId))
        {
            return BookingActionResult.Fail("This booking has no Stripe payment — cancel with a €0 refund and reconcile the money manually.");
        }

        try
        {
            // Refund first (mirrors the guest self-cancel flow); the charge.refunded webhook then drives
            // Refunded/PartiallyRefunded. The cancel itself frees the dates immediately.
            if (refundAmountEur > 0)
            {
                await refunds.RefundAsync(booking.Id, refundAmountEur, "owner_cancellation", cancellationToken);
            }

            await lifecycle.CancelAndReleaseAsync(booking.Id, reason, cancellationToken);
        }
        catch (InvalidBookingStatusTransitionException)
        {
            return BookingActionResult.Fail("This booking can no longer be cancelled.");
        }

        if (refundAmountEur > 0)
        {
            // Bookkeeping on the now-cancelled booking (the commission is voided either way, ADR 0020).
            var cancelled = await dbContext.Bookings.FirstAsync(b => b.Id == booking.Id, cancellationToken);
            cancelled.RecordRefund(new Money(refundAmountEur, cancelled.Total.Currency));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // The guest learns their booking was cancelled; the property manager mirrors the freed dates.
        await emails.EnqueueAsync(id, BookingEmailKind.Cancellation, cancellationToken);
        await emails.EnqueueAsync(id, BookingEmailKind.PropertyManager, cancellationToken);
        return BookingActionResult.Success;
    }

    public async Task<BookingActionResult> SetNotesAsync(Guid id, string? notes, CancellationToken cancellationToken)
    {
        var booking = await FindAsync(id, cancellationToken);
        if (booking is null)
        {
            return BookingActionResult.Fail("Booking not found.");
        }

        booking.SetNotes(notes);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BookingActionResult.Success;
    }

    public async Task<BookingActionResult> MarkSyncedAsync(Guid id, string? note, CancellationToken cancellationToken)
    {
        var booking = await FindAsync(id, cancellationToken);
        if (booking is null)
        {
            return BookingActionResult.Fail("Booking not found.");
        }

        booking.MarkExternalChannelSynced(clock.GetUtcNow().UtcDateTime, note);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BookingActionResult.Success;
    }

    public async Task<ManualBookingResult> CreateManualAsync(ManualBookingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!ManualPaymentMethods.IsValid(command.PaymentMethod))
        {
            return ManualBookingResult.Fail("Pick a valid payment method.");
        }

        if (command.CheckOut <= command.CheckIn)
        {
            return ManualBookingResult.Fail("Check-out must be after check-in.");
        }

        var guests = new GuestParty(command.Adults, command.Children, command.Infants);

        // Pre-quote to validate the agreed amount's bounds (creation re-quotes internally as always).
        if (command.AmountReceivedEur is { } agreed)
        {
            var quote = await quotes.QuoteAsync(
                new QuoteRequest(command.CheckIn, command.CheckOut, guests), cancellationToken);
            var fees = quote.CleaningFee.Amount + quote.TouristTax.Amount;
            if (agreed < fees || agreed > quote.Total.Amount)
            {
                return ManualBookingResult.Fail(
                    $"The agreed amount must cover the fees (€{fees:0.00}) and cannot exceed the quoted total (€{quote.Total.Amount:0.00}).");
            }
        }

        Booking booking;
        try
        {
            booking = await creation.CreateAsync(
                new CreateBookingCommand(
                    command.CheckIn,
                    command.CheckOut,
                    guests,
                    command.GuestName,
                    command.GuestEmail,
                    command.GuestPhone,
                    command.GuestCountry,
                    command.GuestLanguage,
                    PromoCode: command.PromoCode,
                    SpecialRequests: command.SpecialRequests,
                    TotalOverrideEur: command.AmountReceivedEur),
                cancellationToken);
        }
        catch (DatesUnavailableException)
        {
            return ManualBookingResult.Fail("Those dates are no longer available (an existing booking or block overlaps).");
        }
        catch (ArgumentException ex)
        {
            return ManualBookingResult.Fail(ex.Message);
        }

        // No Stripe for a phone/walk-in booking: the owner took the payment, so confirm immediately.
        var tracked = await dbContext.Bookings.FirstAsync(b => b.Id == booking.Id, cancellationToken);
        tracked.ConfirmPayment(command.PaymentMethod, clock.GetUtcNow().UtcDateTime);
        if (!string.IsNullOrWhiteSpace(command.Notes))
        {
            tracked.SetNotes(command.Notes);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (command.SendGuestConfirmation)
        {
            await emails.EnqueueAsync(booking.Id.Value, BookingEmailKind.Confirmation, cancellationToken);
        }

        await emails.EnqueueAsync(booking.Id.Value, BookingEmailKind.OwnerNotification, cancellationToken);
        await emails.EnqueueAsync(booking.Id.Value, BookingEmailKind.PropertyManager, cancellationToken);

        // Stage 8 Part A: a phone guest citing a partner's code earns that partner the commission too.
        await partnerAttribution.OnBookingConfirmedAsync(booking.Id.Value, cancellationToken);
        return new ManualBookingResult(true, null, booking.Id.Value);
    }

    private async Task<BookingActionResult> TransitionAsync(
        Guid id, Action<Booking> transition, string verb, CancellationToken cancellationToken)
    {
        var booking = await FindAsync(id, cancellationToken);
        if (booking is null)
        {
            return BookingActionResult.Fail("Booking not found.");
        }

        try
        {
            transition(booking);
        }
        catch (InvalidBookingStatusTransitionException)
        {
            return BookingActionResult.Fail($"Cannot {verb} a booking that is {booking.Status}.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return BookingActionResult.Success;
    }

    private Task<Booking?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var bookingId = BookingId.From(id);
        return dbContext.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
    }
}
