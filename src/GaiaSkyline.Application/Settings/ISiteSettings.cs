namespace GaiaSkyline.Application.Settings;

/// <summary>The well-known setting keys (Stage 7 §12). A missing row falls back to configuration.</summary>
public static class SettingKeys
{
    public const string PricingProvider = "pricing.provider";
    public const string PricingApiKey = "pricing.api-key"; // secret
    public const string PricingListingId = "pricing.listing-id";
    public const string PricingAdjustmentPct = "pricing.adjustment-pct";
    public const string PricingFloor = "pricing.floor";
    public const string PricingCeiling = "pricing.ceiling";
    public const string PricingSyncIntervalHours = "pricing.sync-interval-hours";
    public const string PropertyManagerEmails = "notifications.pm-emails"; // semicolon-separated
    public const string OwnerEmail = "notifications.owner-email";
    public const string EnabledLanguages = "languages.enabled"; // comma-separated slugs

    // Stage 8 Part A — defaults for new partners and the payout run (ADR 0021).
    public const string PartnerDefaultDiscountPct = "partners.default-discount-pct"; // default 5
    public const string PartnerDefaultCommissionPct = "partners.default-commission-pct"; // default 10
    public const string PartnerMinPayoutEur = "partners.min-payout-eur"; // default 50
}

/// <summary>
/// Read side of the owner-editable settings: a singleton in-memory snapshot of the SiteSettings table,
/// reloaded after every write (and lazily on first use). Secret values are decrypted on read here —
/// callers that DISPLAY them must use the masked accessor instead.
/// </summary>
public interface ISiteSettings
{
    /// <summary>The decrypted value, or null when unset (fall back to configuration).</summary>
    string? Get(string key);

    /// <summary>Masked display form for secrets ("••••1234"), or null when unset.</summary>
    string? GetMasked(string key);

    /// <summary>Drops the snapshot so the next read sees fresh rows (called after writes).</summary>
    void Reload();
}

/// <summary>Write side (scoped): persists a setting (encrypting secrets) and reloads the snapshot.</summary>
public interface ISiteSettingsWriter
{
    /// <summary>Upserts <paramref name="key"/>; null/blank <paramref name="value"/> deletes the override.</summary>
    Task SetAsync(string key, string? value, bool isSecret, CancellationToken cancellationToken);
}
