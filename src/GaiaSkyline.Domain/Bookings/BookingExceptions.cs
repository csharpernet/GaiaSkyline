using GaiaSkyline.Domain.Common;

namespace GaiaSkyline.Domain.Bookings;

/// <summary>Thrown when an illegal booking status transition is attempted.</summary>
public sealed class InvalidBookingStatusTransitionException : DomainException
{
    public InvalidBookingStatusTransitionException(BookingStatus from, BookingStatus to)
        : base($"A booking cannot move from {from} to {to}.")
    {
        From = from;
        To = to;
    }

    public BookingStatus From { get; }

    public BookingStatus To { get; }
}

/// <summary>Thrown when a booking's dates are no longer available (double-booking guard).</summary>
public sealed class DatesUnavailableException : DomainException
{
    public DatesUnavailableException()
        : base("Those dates were just taken.")
    {
    }
}

/// <summary>Thrown when a booking's stated total does not equal the sum of its line items.</summary>
public sealed class BookingTotalMismatchException : DomainException
{
    public BookingTotalMismatchException(decimal expected, decimal actual)
        : base($"Booking total {actual} does not equal the sum of its components {expected}.")
    {
    }
}
