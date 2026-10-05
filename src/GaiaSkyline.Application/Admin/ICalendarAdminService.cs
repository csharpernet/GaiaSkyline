namespace GaiaSkyline.Application.Admin;

/// <summary>What a calendar cell item is — mirrors <see cref="DayOccupancy"/> but keeps identity.</summary>
public enum CalendarItemKind
{
    DirectBooking,
    ImportedBlock,
    ExternalOwnerBlock,
    OwnerUnavailable,
}

/// <summary>
/// One thing occupying calendar dates: a direct booking, an imported iCal block, or an owner block.
/// <see cref="EndDateExclusive"/> is always exclusive (imported blocks are normalized from their
/// inclusive storage). <see cref="InIcsExport"/> says whether the ICS feed carries it — bookings and
/// OwnerUnavailable blocks are exported; ExternalBooking blocks and imported blocks are not (they
/// already live in the management system).
/// </summary>
public sealed record CalendarItem(
    CalendarItemKind Kind,
    string Label,
    DateOnly StartDate,
    DateOnly EndDateExclusive,
    bool InIcsExport,
    Guid? OwnerBlockId = null,
    Guid? BookingId = null,
    string? Note = null);

public sealed record CalendarDayCell(DateOnly Date, bool InPeriod, IReadOnlyList<CalendarItem> Items);

/// <summary>An unresolved imported-block/direct-booking collision, with the booking id for linking.</summary>
public sealed record OpenConflict(
    Guid Id,
    string BookingReference,
    Guid? BookingId,
    string SourceName,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime DetectedAtUtc);

/// <summary>Everything the admin calendar page renders for one month or week.</summary>
public sealed record CalendarPeriod(
    DateOnly From,
    DateOnly ToExclusive,
    IReadOnlyList<CalendarDayCell> Cells,
    IReadOnlyList<CalendarItem> Blocks,
    bool ManualMode,
    string CalendarStatus,
    IReadOnlyList<OpenConflict> OpenConflicts,
    int DuplicateCount);

/// <summary>Reads and conflict actions for the admin calendar (Stage 7 §7). Blocks are written via
/// <see cref="Availability.IOwnerBlockService"/>; re-imports via <see cref="Availability.IExternalCalendarImporter"/>.</summary>
public interface ICalendarAdminService
{
    /// <summary>The Monday-first grid of full weeks covering <paramref name="anchor"/>'s month.</summary>
    Task<CalendarPeriod> GetMonthAsync(DateOnly anchor, CancellationToken cancellationToken);

    /// <summary>The Monday-first week containing <paramref name="anchor"/>.</summary>
    Task<CalendarPeriod> GetWeekAsync(DateOnly anchor, CancellationToken cancellationToken);

    /// <summary>Marks a conflict handled. Returns false when it does not exist.</summary>
    Task<bool> ResolveConflictAsync(Guid id, string? note, CancellationToken cancellationToken);
}
