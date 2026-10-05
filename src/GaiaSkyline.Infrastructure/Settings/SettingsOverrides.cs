using System.Globalization;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Application.Settings;

namespace GaiaSkyline.Infrastructure.Settings;

/// <summary>
/// Applies the owner's DB-backed setting overrides onto configuration-bound options (Stage 7 §12).
/// Null/absent settings leave the configured default in place. Consumers of these options must use
/// IOptionsSnapshot/IOptionsMonitor — plain IOptions is computed once and would never see changes.
/// </summary>
internal static class SettingsOverrides
{
    public static void Apply(PricingProviderOptions options, ISiteSettings settings)
    {
        if (settings.Get(SettingKeys.PricingProvider) is { } provider
            && Enum.TryParse<RateProviderType>(provider, ignoreCase: true, out var parsed))
        {
            options.Provider = parsed;
        }

        if (settings.Get(SettingKeys.PricingApiKey) is { } apiKey)
        {
            options.ApiKey = apiKey;
        }

        if (settings.Get(SettingKeys.PricingListingId) is { } listing)
        {
            options.ListingId = listing;
        }

        if (TryDecimal(settings.Get(SettingKeys.PricingAdjustmentPct), out var adjustment))
        {
            options.DirectBookingAdjustmentPct = adjustment;
        }

        if (TryDecimal(settings.Get(SettingKeys.PricingFloor), out var floor))
        {
            options.FloorPrice = floor;
        }

        if (TryDecimal(settings.Get(SettingKeys.PricingCeiling), out var ceiling))
        {
            options.CeilingPrice = ceiling;
        }

        if (int.TryParse(settings.Get(SettingKeys.PricingSyncIntervalHours), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours)
            && hours >= 1)
        {
            options.SyncIntervalHours = hours;
        }
    }

    public static void Apply(PropertyManagerOptions options, ISiteSettings settings)
    {
        if (settings.Get(SettingKeys.PropertyManagerEmails) is { } emails)
        {
            options.NotificationEmails = emails
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }
    }

    public static void Apply(EmailOptions options, ISiteSettings settings)
    {
        if (settings.Get(SettingKeys.OwnerEmail) is { } owner && owner.Contains('@', StringComparison.Ordinal))
        {
            options.OwnerAddress = owner;
        }
    }

    private static bool TryDecimal(string? text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
