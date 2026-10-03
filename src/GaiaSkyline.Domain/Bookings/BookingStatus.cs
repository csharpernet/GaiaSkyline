namespace GaiaSkyline.Domain.Bookings;

/// <summary>The lifecycle state of a booking. Allowed transitions are defined by
/// <see cref="BookingStatusTransitions"/>.</summary>
public enum BookingStatus
{
    /// <summary>Created, dates held, waiting for a payment to succeed.</summary>
    AwaitingPayment,

    /// <summary>Payment succeeded; the stay is booked.</summary>
    Confirmed,

    /// <summary>Released before or instead of a stay (payment expired/failed, or guest/host cancelled).</summary>
    Cancelled,

    /// <summary>The guest has arrived.</summary>
    CheckedIn,

    /// <summary>The stay has finished.</summary>
    Completed,

    /// <summary>The whole amount has been refunded.</summary>
    Refunded,

    /// <summary>Part of the amount has been refunded.</summary>
    PartiallyRefunded,
}
