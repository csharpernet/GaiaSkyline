using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Availability;

/// <summary>
/// Why a manually-entered block exists. <see cref="ExternalBooking"/> mirrors a reservation that lives
/// in the management system (Hostify) — it blocks our calendar but is NOT exported over iCal (it would
/// echo back once iCal sync is connected). <see cref="OwnerUnavailable"/> is the owner holding dates
/// (maintenance, personal use) and IS exported.
/// </summary>
public enum OwnerBlockKind
{
    ExternalBooking,
    OwnerUnavailable,
}

/// <summary>
/// A date range the owner blocks by hand while the property is synced manually with the management
/// company. Blocks nights the same way a confirmed booking does. <see cref="EndDate"/> is exclusive
/// (the first free night), matching how stays are expressed.
/// </summary>
public sealed class OwnerBlock : Entity<OwnerBlockId>
{
    // Required by EF Core's materialization.
    private OwnerBlock()
    {
    }

    public OwnerBlock(
        OwnerBlockId id,
        DateOnly startDate,
        DateOnly endDateExclusive,
        OwnerBlockKind kind,
        string? note,
        DateTime createdAtUtc,
        string createdBy)
    {
        if (endDateExclusive <= startDate)
        {
            throw new ArgumentException("Owner block end date must be after the start date (exclusive).", nameof(endDateExclusive));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        Id = id;
        StartDate = startDate;
        EndDate = endDateExclusive;
        Kind = kind;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy.Trim();
    }

    public DateOnly StartDate { get; private set; }

    /// <summary>Exclusive — the first night that is free again.</summary>
    public DateOnly EndDate { get; private set; }

    public OwnerBlockKind Kind { get; private set; }

    public string? Note { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    /// <summary>Whether <paramref name="night"/> falls within this block (half-open [Start, End)).</summary>
    public bool Covers(DateOnly night) => night >= StartDate && night < EndDate;

    /// <summary>Whether this block overlaps the half-open stay [checkIn, checkOut).</summary>
    public bool Overlaps(DateOnly checkIn, DateOnly checkOut) => StartDate < checkOut && checkIn < EndDate;
}
