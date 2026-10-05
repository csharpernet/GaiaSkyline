using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>
/// Owner booking actions for the admin manager (Stage 7 §6). Transitions go through the domain status
/// machine; an illegal move is returned as a failed <see cref="BookingActionResult"/> rather than thrown.
/// Cancellation delegates to <see cref="IBookingLifecycleService"/> so the held nights are released.
/// </summary>
internal sealed class AdminBookingService(
    AppDbContext dbContext,
    IBookingLifecycleService lifecycle,
    TimeProvider clock) : IAdminBookingService
{
    public Task<BookingActionResult> CheckInAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, b => b.CheckInGuest(), "check in", cancellationToken);

    public Task<BookingActionResult> CompleteAsync(Guid id, CancellationToken cancellationToken) =>
        TransitionAsync(id, b => b.Complete(), "complete", cancellationToken);

    public Task<BookingActionResult> RefundAsync(Guid id, bool partial, CancellationToken cancellationToken) =>
        TransitionAsync(id, b =>
        {
            if (partial)
            {
                b.MarkPartiallyRefunded();
            }
            else
            {
                b.MarkRefunded();
            }
        }, partial ? "partially refund" : "refund", cancellationToken);

    public async Task<BookingActionResult> CancelAsync(Guid id, string? reason, CancellationToken cancellationToken)
    {
        var bookingId = BookingId.From(id);
        if (!await dbContext.Bookings.AsNoTracking().AnyAsync(b => b.Id == bookingId, cancellationToken))
        {
            return BookingActionResult.Fail("Booking not found.");
        }

        try
        {
            await lifecycle.CancelAndReleaseAsync(bookingId, reason, cancellationToken);
            return BookingActionResult.Success;
        }
        catch (InvalidBookingStatusTransitionException)
        {
            return BookingActionResult.Fail("This booking can no longer be cancelled.");
        }
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
