using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Availability;

/// <summary>
/// A date range blocked by an external calendar (Airbnb, Booking.com, …) imported over iCal. The
/// table exists now so the availability query can union it in; it is only populated by the ICS sync
/// built in Stage 5. Active rows block their nights the same way confirmed bookings do.
/// </summary>
public sealed class ExternalCalendarBlock : Entity<ExternalCalendarBlockId>
{
    // Required by EF Core's materialization.
    private ExternalCalendarBlock()
    {
    }

    public ExternalCalendarBlock(
        ExternalCalendarBlockId id,
        string source,
        DateOnly startDate,
        DateOnly endDate,
        DateTime createdAtUtc,
        string? externalUid = null,
        string? summary = null,
        ExternalCalendarSourceId? sourceId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (endDate < startDate)
        {
            throw new ArgumentException("External block end date must be on or after the start date.", nameof(endDate));
        }

        Id = id;
        Source = source.Trim();
        SourceId = sourceId;
        StartDate = startDate;
        EndDate = endDate;
        ExternalUid = string.IsNullOrWhiteSpace(externalUid) ? null : externalUid.Trim();
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        IsActive = true;
        CreatedAtUtc = createdAtUtc;
        LastSeenAtUtc = createdAtUtc;
    }

    /// <summary>The originating calendar name (e.g. "Hostify"); a snapshot of the source's name.</summary>
    public string Source { get; private set; } = null!;

    /// <summary>The configured source this block was imported from (null for legacy/seeded rows).</summary>
    public ExternalCalendarSourceId? SourceId { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>Inclusive last blocked night.</summary>
    public DateOnly EndDate { get; private set; }

    /// <summary>The source calendar's UID for this event, used to de-duplicate on re-sync.</summary>
    public string? ExternalUid { get; private set; }

    public string? Summary { get; private set; }

    /// <summary>Whether this block still applies (cleared when it disappears from the source feed).</summary>
    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>When this block was last seen in a sync (used to expire stale rows).</summary>
    public DateTime LastSeenAtUtc { get; private set; }

    /// <summary>Refreshes the range/summary from the source feed and marks it seen + active.</summary>
    public void Refresh(DateOnly startDate, DateOnly endDate, string? summary, DateTime seenAtUtc)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("External block end date must be on or after the start date.", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
        Summary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        IsActive = true;
        LastSeenAtUtc = seenAtUtc;
    }

    /// <summary>Soft-deletes the block when it has disappeared from the source feed.</summary>
    public void Deactivate() => IsActive = false;
}
