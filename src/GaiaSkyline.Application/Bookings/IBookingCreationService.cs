using GaiaSkyline.Domain.Bookings;

namespace GaiaSkyline.Application.Bookings;

/// <summary>
/// Creates a booking in <see cref="BookingStatus.AwaitingPayment"/> with its nights held. Runs in a
/// Serializable transaction that re-checks availability before inserting; a conflict surfaces as
/// <see cref="DatesUnavailableException"/>, never a raw SQL error.
/// </summary>
public interface IBookingCreationService
{
    Task<Booking> CreateAsync(CreateBookingCommand command, CancellationToken cancellationToken);
}
