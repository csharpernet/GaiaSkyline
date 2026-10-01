namespace GaiaSkyline.Web.Seo;

/// <summary>A visible + structured breadcrumb trail entry. <see cref="RelativePath"/> is null for the current page.</summary>
public sealed record Breadcrumb(string Label, string? RelativePath);

/// <summary>
/// Everything the SEO &lt;head&gt; needs for a page. Canonical and hreflang alternates are derived
/// from <see cref="Slug"/> + <see cref="RelativePath"/>, so the hreflang graph is complete and
/// reciprocal by construction.
/// </summary>
public sealed class PageMeta
{
    /// <summary>Active BCP-47 culture (e.g. "en", "pt-PT") — used for &lt;html lang&gt;.</summary>
    public required string Culture { get; init; }

    /// <summary>Active URL slug (e.g. "en", "pt-pt").</summary>
    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    /// <summary>Path after the language segment, without a leading slash (e.g. "", "gallery", "stories/x").</summary>
    public required string RelativePath { get; init; }

    public string OgType { get; init; } = "website";

    /// <summary>Page-specific OpenGraph image (relative path such as /media/...); made absolute in the layout.</summary>
    public string? OgImagePath { get; init; }

    public IReadOnlyList<Breadcrumb> Breadcrumbs { get; init; } = [];

    /// <summary>Raw JSON-LD to inject in a &lt;script type="application/ld+json"&gt; block.</summary>
    public string? JsonLd { get; init; }
}
