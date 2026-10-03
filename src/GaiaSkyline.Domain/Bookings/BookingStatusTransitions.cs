namespace GaiaSkyline.Domain.Bookings;

/// <summary>
/// The booking status machine. Only the edges listed here are legal; every other transition
/// (including a status to itself) throws <see cref="InvalidBookingStatusTransitionException"/>.
/// </summary>
public static class BookingStatusTransitions
{
    private static readonly Dictionary<BookingStatus, IReadOnlySet<BookingStatus>> Allowed =
        new()
        {
            [BookingStatus.AwaitingPayment] = new HashSet<BookingStatus>
            {
                BookingStatus.Confirmed,
                BookingStatus.Cancelled,
            },
            [BookingStatus.Confirmed] = new HashSet<BookingStatus>
            {
                BookingStatus.CheckedIn,
                BookingStatus.Cancelled,
                BookingStatus.Refunded,
                BookingStatus.PartiallyRefunded,
            },
            [BookingStatus.CheckedIn] = new HashSet<BookingStatus>
            {
                BookingStatus.Completed,
                BookingStatus.Refunded,
                BookingStatus.PartiallyRefunded,
            },
            [BookingStatus.Completed] = new HashSet<BookingStatus>
            {
                BookingStatus.Refunded,
                BookingStatus.PartiallyRefunded,
            },
            [BookingStatus.Cancelled] = new HashSet<BookingStatus>
            {
                BookingStatus.Refunded,
                BookingStatus.PartiallyRefunded,
            },
            [BookingStatus.PartiallyRefunded] = new HashSet<BookingStatus>
            {
                BookingStatus.Refunded,
            },
            [BookingStatus.Refunded] = new HashSet<BookingStatus>(),
        };

    /// <summary>Whether <paramref name="from"/> → <paramref name="to"/> is a legal transition.</summary>
    public static bool CanTransition(BookingStatus from, BookingStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Throws <see cref="InvalidBookingStatusTransitionException"/> if the transition is illegal.</summary>
    public static void EnsureCanTransition(BookingStatus from, BookingStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidBookingStatusTransitionException(from, to);
        }
    }
}
