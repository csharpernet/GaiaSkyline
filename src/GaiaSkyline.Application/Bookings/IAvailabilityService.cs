namespace GaiaSkyline.Application.Bookings;

/// <summary>
/// Computes blocked nights = booked occupancy (bookings awaiting payment / confirmed / checked-in)
/// unioned with active external-calendar blocks. Results are cached per month (60s) and invalidated
/// whenever a booking's state changes.
/// </summary>
public interface IAvailabilityService
{
    /// <summary>The blocked nights within [<paramref name="from"/>, <paramref name="to"/>).</summary>
    Task<IReadOnlyList<DateOnly>> GetBlockedDatesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Drops cached availability (called after any booking state change).</summary>
    void Invalidate();
}
