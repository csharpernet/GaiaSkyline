using GaiaSkyline.Application.Seo;

namespace GaiaSkyline.Web.Models;

/// <summary>One page+language meta override posted from the SEO editor.</summary>
public sealed class PageMetaForm
{
    public string PageKey { get; set; } = string.Empty;

    public string LanguageCode { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Description { get; set; }
}

/// <summary>The /admin/seo page: redirect rules plus the per-page, per-language meta overrides.</summary>
public sealed record SeoAdminViewModel(
    IReadOnlyList<RedirectDto> Redirects,
    IReadOnlyList<PageMetaOverrideDto> PageMeta);
