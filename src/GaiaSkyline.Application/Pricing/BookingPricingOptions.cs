namespace GaiaSkyline.Application.Pricing;

/// <summary>
/// Configuration for the initial seeded pricing and the runtime last-minute rule, bound from the
/// <c>Booking:Pricing</c> section. The nightly rate here is the owner's starting value (kept aligned
/// with the Airbnb listing); after seeding, prices are edited in the backend (Stage 7 admin), which
/// is the source of truth. See ADR 0009.
/// </summary>
public sealed class BookingPricingOptions
{
    public const string SectionName = "Booking:Pricing";

    /// <summary>Starting nightly rate in euros. Set this to match the Airbnb listing.</summary>
    public decimal BaseNightlyRateEur { get; set; }

    /// <summary>Cleaning fee per stay, in euros.</summary>
    public decimal CleaningFeeEur { get; set; }

    /// <summary>Standard minimum nights (seeded onto the pricing rule).</summary>
    public int StandardMinNights { get; set; } = 3;

    /// <summary>Within this many days of check-in, the minimum drops to <see cref="LastMinuteMinNights"/>.</summary>
    public int LastMinuteWindowDays { get; set; } = 7;

    /// <summary>Minimum nights for last-minute (gap-filling) bookings.</summary>
    public int LastMinuteMinNights { get; set; } = 1;

    /// <summary>Seeded weekly (7+ nights) discount percent; backend-managed afterward (0 = none).</summary>
    public int WeeklyDiscountPct { get; set; }

    /// <summary>Seeded monthly (28+ nights) discount percent; backend-managed afterward (0 = none).</summary>
    public int MonthlyDiscountPct { get; set; }

    /// <summary>Municipal tourist tax per adult per night, in euros. Null/0 = no separate tax.</summary>
    public decimal? TouristTaxPerAdultPerNightEur { get; set; }

    /// <summary>Nights cap for the tourist tax.</summary>
    public int? TouristTaxMaxNights { get; set; }

    /// <summary>Documented exempt age for the tourist tax.</summary>
    public int? TouristTaxMinAgeExempt { get; set; }

    /// <summary>Cancellation refund tiers (defaults to Flexible: 100% until 1 day before check-in).</summary>
    public List<CancellationTierOption> CancellationTiers { get; set; } =
        [new CancellationTierOption { DaysBeforeCheckIn = 1, RefundPct = 100 }];
}

/// <summary>A configured cancellation tier (see <see cref="BookingPricingOptions.CancellationTiers"/>).</summary>
public sealed class CancellationTierOption
{
    public int DaysBeforeCheckIn { get; set; }

    public int RefundPct { get; set; }
}
