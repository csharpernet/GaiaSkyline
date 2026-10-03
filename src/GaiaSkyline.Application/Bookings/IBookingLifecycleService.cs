using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Application.Bookings;

/// <summary>Booking state changes that must release held nights atomically.</summary>
public interface IBookingLifecycleService
{
    /// <summary>
    /// Cancels a booking and deletes its occupancy rows in one transaction, freeing the dates.
    /// Used by guest/host cancellation and by the unpaid-hold expiry job.
    /// </summary>
    Task CancelAndReleaseAsync(BookingId bookingId, string? reason, CancellationToken cancellationToken);
}
