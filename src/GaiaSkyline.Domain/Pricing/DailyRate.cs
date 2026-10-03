using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Pricing;

/// <summary>Where a daily rate came from. Owner-set rates are Manual; imports are PriceLabs/Hostify.</summary>
public enum RateSource
{
    Manual,
    PriceLabs,
    Hostify,
}

/// <summary>
/// A per-date nightly rate (and optional minimum nights), keyed by the date. Takes precedence over
/// season rules and the base rate. Owner-locked dates are never overwritten by an automatic import.
/// </summary>
public sealed class DailyRate
{
    // Required by EF Core's materialization.
    private DailyRate()
    {
    }

    public DailyRate(
        DateOnly date,
        Money nightlyRate,
        int? minNights,
        RateSource source,
        DateTime updatedAtUtc,
        string updatedBy,
        bool isLockedByOwner = false,
        DateTime? sourceUpdatedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
        if (minNights is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minNights), "Minimum nights must be at least 1.");
        }

        Date = date;
        NightlyRate = nightlyRate;
        MinNights = minNights;
        Source = source;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy.Trim();
        IsLockedByOwner = isLockedByOwner;
        SourceUpdatedAtUtc = sourceUpdatedAtUtc;
    }

    public DateOnly Date { get; private set; }

    public Money NightlyRate { get; private set; }

    public int? MinNights { get; private set; }

    public RateSource Source { get; private set; }

    public DateTime? SourceUpdatedAtUtc { get; private set; }

    public bool IsLockedByOwner { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; } = null!;

    public void SetRate(Money nightlyRate, int? minNights, RateSource source, DateTime updatedAtUtc, string updatedBy, DateTime? sourceUpdatedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
        NightlyRate = nightlyRate;
        MinNights = minNights;
        Source = source;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy.Trim();
        SourceUpdatedAtUtc = sourceUpdatedAtUtc;
    }

    public void SetLocked(bool locked, DateTime updatedAtUtc, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
        IsLockedByOwner = locked;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy.Trim();
    }
}
