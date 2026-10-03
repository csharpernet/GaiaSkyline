using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Bookings;

/// <summary>
/// One row per occupied night, with the night (<see cref="Date"/>) as the primary key. This is the
/// database-level double-booking guard: a second booking for the same night fails the primary-key
/// insert, rolling its transaction back. Rows are inserted with the booking and deleted when its
/// dates are released (cancellation or payment expiry), in the same transaction.
/// </summary>
public sealed class BookingDateOccupancy
{
    // Required by EF Core's materialization.
    private BookingDateOccupancy()
    {
    }

    public BookingDateOccupancy(DateOnly date, BookingId bookingId)
    {
        Date = date;
        BookingId = bookingId;
    }

    /// <summary>The occupied night. Primary key.</summary>
    public DateOnly Date { get; private set; }

    /// <summary>The booking that holds this night.</summary>
    public BookingId BookingId { get; private set; }
}
