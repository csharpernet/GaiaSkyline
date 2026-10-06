using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Seo;
using GaiaSkyline.Web.Localization;

namespace GaiaSkyline.Web.Seo;

/// <summary>One page in the sitemap: its site-relative path and the per-language URLs (a language is excluded
/// when the owner marked that page+language noindex in the SEO admin). Stage 7 §5.</summary>
public sealed record SitemapEntry(string Path, IReadOnlyList<SitemapEntryLanguage> Languages);

/// <summary>A single language variant of a sitemap page; <see cref="Included"/> is false when it is noindex.</summary>
public sealed record SitemapEntryLanguage(string Culture, string Slug, string Url, bool Included);

/// <summary>
/// Builds the structured sitemap model shared by the public <c>/sitemap.xml</c> and the owner's sitemap preview
/// on <c>/admin/seo</c>. Pages the owner marked noindex (per language) are flagged so the XML can drop them and
/// the preview can show why. Stage 7 §5.
/// </summary>
public sealed class SitemapBuilder(IContentService content, IPageMetaResolver pageMeta, GaiaSkyline.Web.Localization.IEnabledLanguages enabledLanguages)
{
    /// <summary>The fixed public pages, keyed by site-relative path (matches <c>SeoPages</c> and the resolver keys).</summary>
    private static readonly string[] StaticPaths =
    [
        "",
        "gallery",
        "book",
        "stories",
        "legal/terms",
        "legal/privacy",
        "legal/cancellation-policy",
        "legal/al-registration",
    ];

    public async Task<IReadOnlyList<SitemapEntry>> BuildAsync(string baseUrl, CancellationToken cancellationToken)
    {
        var stories = await content.GetPublishedStoriesAsync(SupportedCultures.DefaultCulture, take: null, cancellationToken);
        var noIndex = await pageMeta.GetNoIndexKeysAsync(cancellationToken);

        var entries = new List<SitemapEntry>(StaticPaths.Length + stories.Count);
        foreach (var path in StaticPaths)
        {
            entries.Add(BuildEntry(baseUrl, path, noIndex));
        }

        // Story detail pages have no per-page meta overrides, so every language is always included —
        // each under its own per-language slug (Stage 7 §4).
        foreach (var story in stories)
        {
            var languages = enabledLanguages.All
                .Select(c => new SitemapEntryLanguage(
                    c.Culture,
                    c.Slug,
                    AbsoluteUrl(baseUrl, c.Slug, $"stories/{story.SlugByLanguage.GetValueOrDefault(c.Culture, story.Slug)}"),
                    Included: true))
                .ToList();
            entries.Add(new SitemapEntry($"stories/{story.Slug}", languages));
        }

        return entries;
    }

    private SitemapEntry BuildEntry(string baseUrl, string path, IReadOnlySet<string> noIndex)
    {
        var key = path.ToLowerInvariant();
        // Only owner-enabled languages appear in the sitemap (Stage 7 §12).
        var languages = enabledLanguages.All
            .Select(c => new SitemapEntryLanguage(
                c.Culture,
                c.Slug,
                AbsoluteUrl(baseUrl, c.Slug, path),
                Included: !noIndex.Contains($"{key}|{c.Culture}")))
            .ToList();
        return new SitemapEntry(path, languages);
    }

    private static string AbsoluteUrl(string baseUrl, string slug, string path) =>
        string.IsNullOrEmpty(path) ? $"{baseUrl}/{slug}" : $"{baseUrl}/{slug}/{path}";
}
