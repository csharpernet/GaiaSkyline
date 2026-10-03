using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Pricing;

/// <summary>
/// A nightly rate (and length-of-stay discounts) that applies to a closed date range
/// [<see cref="StartDate"/>, <see cref="EndDate"/>]. A stay that spans several rules is priced
/// per night from the rule covering each night; overlapping rules are a seed-time error.
/// </summary>
public sealed class PricingRule : Entity<PricingRuleId>
{
    // Required by EF Core's materialization.
    private PricingRule()
    {
    }

    public PricingRule(
        PricingRuleId id,
        DateOnly startDate,
        DateOnly endDate,
        Money nightlyRate,
        int minNights,
        int weeklyDiscountPct,
        int monthlyDiscountPct)
    {
        if (endDate < startDate)
        {
            throw new ArgumentException("Pricing rule end date must be on or after the start date.", nameof(endDate));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(nightlyRate.Amount);
        ArgumentOutOfRangeException.ThrowIfLessThan(minNights, 1);
        ThrowIfNotPercentage(weeklyDiscountPct, nameof(weeklyDiscountPct));
        ThrowIfNotPercentage(monthlyDiscountPct, nameof(monthlyDiscountPct));

        Id = id;
        StartDate = startDate;
        EndDate = endDate;
        NightlyRate = nightlyRate;
        MinNights = minNights;
        WeeklyDiscountPct = weeklyDiscountPct;
        MonthlyDiscountPct = monthlyDiscountPct;
    }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public Money NightlyRate { get; private set; }

    public int MinNights { get; private set; }

    /// <summary>Discount (percent) applied to stays of 7+ nights.</summary>
    public int WeeklyDiscountPct { get; private set; }

    /// <summary>Discount (percent) applied to stays of 28+ nights.</summary>
    public int MonthlyDiscountPct { get; private set; }

    /// <summary>Whether this rule covers the given night (a night is identified by its date).</summary>
    public bool Covers(DateOnly night) => night >= StartDate && night <= EndDate;

    private static void ThrowIfNotPercentage(int value, string paramName)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value, paramName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 100, paramName);
    }
}
