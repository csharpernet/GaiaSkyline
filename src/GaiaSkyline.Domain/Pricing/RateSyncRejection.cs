using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Pricing;

/// <summary>How the owner settled a rejected provider price.</summary>
public enum RateSyncRejectionStatus
{
    Open,
    Accepted,
    Dismissed,
}

/// <summary>
/// A provider price the rate sync refused to apply (outside the owner's floor/ceiling bounds),
/// kept for review: the owner can accept it as a manual price or dismiss it (Stage 7 §8). One open
/// rejection per date — a later sync for the same date updates the offer instead of piling up rows.
/// </summary>
public sealed class RateSyncRejection : Entity<RateSyncRejectionId>
{
    // Required by EF Core's materialization.
    private RateSyncRejection()
    {
    }

    public RateSyncRejection(
        RateSyncRejectionId id,
        DateOnly date,
        decimal offeredPriceEur,
        int? offeredMinNights,
        string provider,
        string reason,
        DateTime detectedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(offeredPriceEur);

        Id = id;
        Date = date;
        OfferedPriceEur = offeredPriceEur;
        OfferedMinNights = offeredMinNights;
        Provider = provider.Trim();
        Reason = reason.Trim();
        DetectedAtUtc = detectedAtUtc;
        Status = RateSyncRejectionStatus.Open;
    }

    public DateOnly Date { get; private set; }

    public decimal OfferedPriceEur { get; private set; }

    public int? OfferedMinNights { get; private set; }

    /// <summary>The provider that offered the price (e.g. "PriceLabs") — a snapshot, not a FK.</summary>
    public string Provider { get; private set; } = null!;

    /// <summary>Why it was refused (e.g. "below floor €50", "above ceiling €900").</summary>
    public string Reason { get; private set; } = null!;

    public DateTime DetectedAtUtc { get; private set; }

    public RateSyncRejectionStatus Status { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>A later sync offered a new price for the same still-open date.</summary>
    public void UpdateOffer(decimal offeredPriceEur, int? offeredMinNights, string reason, DateTime detectedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(offeredPriceEur);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status != RateSyncRejectionStatus.Open)
        {
            throw new InvalidOperationException("Only an open rejection can receive a new offer.");
        }

        OfferedPriceEur = offeredPriceEur;
        OfferedMinNights = offeredMinNights;
        Reason = reason.Trim();
        DetectedAtUtc = detectedAtUtc;
    }

    public void Accept(DateTime atUtc) => Resolve(RateSyncRejectionStatus.Accepted, atUtc);

    public void Dismiss(DateTime atUtc) => Resolve(RateSyncRejectionStatus.Dismissed, atUtc);

    private void Resolve(RateSyncRejectionStatus status, DateTime atUtc)
    {
        if (Status != RateSyncRejectionStatus.Open)
        {
            throw new InvalidOperationException("This rejection was already settled.");
        }

        Status = status;
        ResolvedAtUtc = atUtc;
    }
}
