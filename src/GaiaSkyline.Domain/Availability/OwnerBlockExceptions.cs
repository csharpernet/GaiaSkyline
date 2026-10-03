namespace GaiaSkyline.Domain.Availability;

/// <summary>
/// Thrown when an owner tries to block dates that overlap an active direct booking. Names the booking
/// reference so the owner knows exactly which reservation stands in the way.
/// </summary>
public sealed class OwnerBlockConflictsWithBookingException(string bookingReference)
    : Exception($"These dates overlap an active booking ({bookingReference}). Cancel or adjust it first.")
{
    public string BookingReference { get; } = bookingReference;
}
