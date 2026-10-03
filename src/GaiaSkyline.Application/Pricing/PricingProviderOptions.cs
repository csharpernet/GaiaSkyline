namespace GaiaSkyline.Application.Pricing;

public enum RateProviderType
{
    None,
    PriceLabs,
    Hostify,
}

/// <summary>
/// Automatic-pricing configuration (<c>Pricing</c> section). Defaults to None (manual pricing). The API
/// key is a credential (encrypt at rest when a real adapter arrives). Imported prices get the
/// direct-booking adjustment and are bounded by the floor/ceiling. See ADR 0014.
/// </summary>
public sealed class PricingProviderOptions
{
    public const string SectionName = "Pricing";

    public RateProviderType Provider { get; set; } = RateProviderType.None;

    public string ApiKey { get; set; } = string.Empty;

    public string ListingId { get; set; } = string.Empty;

    /// <summary>Applied to imported prices only (e.g. -10 for a 10% direct-booking discount). Whole euros.</summary>
    public decimal DirectBookingAdjustmentPct { get; set; }

    public decimal FloorPrice { get; set; } = 1m;

    public decimal CeilingPrice { get; set; } = 100000m;

    /// <summary>How often the sync job runs.</summary>
    public int SyncIntervalHours { get; set; } = 4;
}
