using System.Globalization;
using GaiaSkyline.Web.Localization;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace GaiaSkyline.Web.Controllers;

/// <summary>Base for the language-segmented public pages. Builds and stashes the SEO <see cref="PageMeta"/>.</summary>
[OutputCache(PolicyName = "public")]
public abstract class PublicController : Controller
{
    protected const string BrandName = "Gaia Skyline";

    /// <summary>Active BCP-47 culture, set by the route culture provider from the URL.</summary>
    protected string CurrentCulture => CultureInfo.CurrentUICulture.Name;

    protected string CurrentSlug => SupportedCultures.SlugForCulture(CurrentCulture);

    protected void SetMeta(PageMeta meta) => ViewData["PageMeta"] = meta;

    protected PageMeta Meta(
        string relativePath,
        string title,
        string description,
        string ogType = "website",
        string? ogImagePath = null,
        IReadOnlyList<Breadcrumb>? breadcrumbs = null,
        IReadOnlyList<string>? jsonLdBlocks = null) => new()
        {
            Culture = CurrentCulture,
            Slug = CurrentSlug,
            Title = title,
            Description = description,
            RelativePath = relativePath,
            OgType = ogType,
            OgImagePath = ogImagePath,
            Breadcrumbs = breadcrumbs ?? [],
            JsonLdBlocks = jsonLdBlocks ?? [],
        };

    /// <summary>Absolute base URL for the current request (used to build JSON-LD and OG URLs).</summary>
    protected string BaseUrl => $"{Request.Scheme}://{Request.Host}";
}
