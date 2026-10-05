using GaiaSkyline.Application.Seo;
using GaiaSkyline.Web.Seo;

namespace GaiaSkyline.Web.Models;

/// <summary>One page+language meta override posted from the SEO editor.</summary>
public sealed class PageMetaForm
{
    public string PageKey { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>Unchecked = noindex. Posted as a checkbox, so a missing value means false. Default is indexable.</summary>
    public bool Index { get; set; }

    /// <summary>Unchecked = nofollow. Posted as a checkbox, so a missing value means false. Default is followable.</summary>
    public bool Follow { get; set; }
}

/// <summary>
/// The /admin/seo page: redirect rules, per-page meta/robots overrides, a sitemap preview and the recent
/// Core Web Vitals field data.
/// </summary>
public sealed record SeoAdminViewModel(
    IReadOnlyList<RedirectDto> Redirects,
    IReadOnlyList<PageMetaOverrideDto> PageMeta,
    IReadOnlyList<SitemapEntry> Sitemap,
    IReadOnlyList<WebVitalSummary> Vitals,
    IReadOnlyList<SeoWarning> Warnings);
