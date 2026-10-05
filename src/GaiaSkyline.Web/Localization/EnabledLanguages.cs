using GaiaSkyline.Application.Settings;

namespace GaiaSkyline.Web.Localization;

/// <summary>
/// The owner's per-language toggles (Stage 7 §12), layered over the compile-time
/// <see cref="SupportedCultures"/>. No setting (the default) means every language is enabled; the
/// default language can never be disabled. Disabled languages drop out of routing (public pages 404),
/// the hreflang set, the language switcher and the sitemap.
/// </summary>
public interface IEnabledLanguages
{
    bool IsEnabled(string slug);

    IReadOnlyList<CultureOption> All { get; }
}

public sealed class EnabledLanguages(ISiteSettings settings) : IEnabledLanguages
{
    public bool IsEnabled(string slug)
    {
        if (string.Equals(slug, SupportedCultures.DefaultSlug, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var configured = settings.Get(SettingKeys.EnabledLanguages);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return true;
        }

        return configured
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(slug, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CultureOption> All =>
        SupportedCultures.All.Where(c => IsEnabled(c.Slug)).ToList();
}
