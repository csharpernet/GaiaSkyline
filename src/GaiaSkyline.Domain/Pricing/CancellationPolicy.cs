using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Pricing;

/// <summary>
/// The (singleton) cancellation policy, expressed as refund tiers. Given how many days before
/// check-in a booking is cancelled, the refund is the highest tier the guest still qualifies for;
/// cancelling later than every tier refunds nothing.
/// </summary>
public sealed class CancellationPolicy : Entity<CancellationPolicyId>
{
    private readonly List<CancellationTier> _tiers = [];

    // Required by EF Core's materialization.
    private CancellationPolicy()
    {
    }

    public CancellationPolicy(CancellationPolicyId id, IEnumerable<CancellationTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        var ordered = tiers
            .OrderByDescending(t => t.DaysBeforeCheckIn)
            .ToList();

        if (ordered.Count == 0)
        {
            throw new ArgumentException("A cancellation policy needs at least one tier.", nameof(tiers));
        }

        foreach (var tier in ordered)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(tier.DaysBeforeCheckIn);
            ArgumentOutOfRangeException.ThrowIfNegative(tier.RefundPct);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tier.RefundPct, 100);
        }

        Id = id;
        _tiers.AddRange(ordered);
    }

    /// <summary>The tiers, ordered from the most generous (furthest before check-in) downward.</summary>
    public IReadOnlyList<CancellationTier> Tiers => _tiers.AsReadOnly();

    /// <summary>
    /// The refund percentage (0–100) for a cancellation made <paramref name="daysBeforeCheckIn"/>
    /// days before check-in. Returns the highest tier whose threshold is still met, else 0.
    /// </summary>
    public int RefundPercentageFor(int daysBeforeCheckIn)
    {
        foreach (var tier in _tiers)
        {
            if (daysBeforeCheckIn >= tier.DaysBeforeCheckIn)
            {
                return tier.RefundPct;
            }
        }

        return 0;
    }
}
