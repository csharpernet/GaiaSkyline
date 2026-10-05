namespace GaiaSkyline.Application.Seo;

/// <summary>A public page whose SEO meta the owner can override, keyed by its site-relative path.</summary>
public sealed record SeoPage(string Key, string Label);

/// <summary>The fixed set of public pages with owner-editable per-language meta (each story has its own in §4).</summary>
public static class SeoPages
{
    public static IReadOnlyList<SeoPage> All { get; } =
    [
        new("", "Home"),
        new("gallery", "Photo gallery"),
        new("book", "Book"),
        new("stories", "Stories index"),
        new("legal/terms", "Legal — Terms & Conditions"),
        new("legal/privacy", "Legal — Privacy Policy"),
        new("legal/cancellation-policy", "Legal — Cancellation Policy"),
        new("legal/al-registration", "Legal — AL Registration"),
    ];

    public static bool IsKnown(string key) =>
        All.Any(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
}
