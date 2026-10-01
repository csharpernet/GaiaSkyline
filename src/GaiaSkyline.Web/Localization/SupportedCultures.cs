namespace GaiaSkyline.Web.Localization;

/// <summary>A supported UI language: its URL slug, BCP-47 culture and display metadata.</summary>
public sealed record CultureOption(string Slug, string Culture, string NativeName, string ShortLabel)
{
    /// <summary>OpenGraph locale (e.g. "en", "pt_PT").</summary>
    public string OgLocale => Culture.Replace('-', '_');
}

/// <summary>
/// The five enabled cultures and the mapping between URL slug (lowercase, e.g. "pt-pt") and
/// BCP-47 culture (e.g. "pt-PT"). English is the default and fallback.
/// </summary>
public static class SupportedCultures
{
    public const string DefaultSlug = "en";
    public const string DefaultCulture = "en";

    public static readonly IReadOnlyList<CultureOption> All =
    [
        new("en", "en", "English", "EN"),
        new("pt-pt", "pt-PT", "Português", "PT"),
        new("es", "es", "Español", "ES"),
        new("fr", "fr", "Français", "FR"),
        new("de", "de", "Deutsch", "DE"),
    ];

    private static readonly Dictionary<string, CultureOption> BySlug =
        All.ToDictionary(c => c.Slug, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, CultureOption> ByCulture =
        All.ToDictionary(c => c.Culture, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> AllSlugs => All.Select(c => c.Slug);

    public static IEnumerable<string> AllCultures => All.Select(c => c.Culture);

    public static bool IsValidSlug(string? slug) => slug is not null && BySlug.ContainsKey(slug);

    public static CultureOption? TryGetBySlug(string? slug) =>
        slug is not null && BySlug.TryGetValue(slug, out var option) ? option : null;

    public static CultureOption? TryGetByCulture(string? culture) =>
        culture is not null && ByCulture.TryGetValue(culture, out var option) ? option : null;

    public static string SlugForCulture(string culture) => TryGetByCulture(culture)?.Slug ?? DefaultSlug;
}
