using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Pricing;

/// <summary>
/// A configurable fee. A cleaning fee is a flat per-stay amount; the tourist tax is per paying guest
/// per night, capped at <see cref="MaxNights"/> nights, with guests under <see cref="MinAgeExempt"/>
/// exempt. Because bookings record adults/children/infants (not exact ages), the tax is charged per
/// adult; <see cref="MinAgeExempt"/> records the municipal policy. See ADR 0009.
/// </summary>
public sealed class Fee : Entity<FeeId>
{
    // Required by EF Core's materialization.
    private Fee()
    {
    }

    public Fee(
        FeeId id,
        FeeType type,
        Money amount,
        bool isPerNight,
        bool isPerGuest,
        int? maxNights = null,
        int? minAgeExempt = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount.Amount);
        if (maxNights is { } max)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(max, 1);
        }

        if (minAgeExempt is { } age)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(age);
        }

        Id = id;
        Type = type;
        Amount = amount;
        IsPerNight = isPerNight;
        IsPerGuest = isPerGuest;
        MaxNights = maxNights;
        MinAgeExempt = minAgeExempt;
    }

    public FeeType Type { get; private set; }

    public Money Amount { get; private set; }

    public bool IsPerNight { get; private set; }

    public bool IsPerGuest { get; private set; }

    /// <summary>For per-night fees, the maximum number of nights charged (null = uncapped).</summary>
    public int? MaxNights { get; private set; }

    /// <summary>The age below which a guest is exempt (null = everyone pays). Documents the policy.</summary>
    public int? MinAgeExempt { get; private set; }
}
