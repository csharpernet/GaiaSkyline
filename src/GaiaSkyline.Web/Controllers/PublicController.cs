using System.Globalization;
using GaiaSkyline.Web.Localization;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>Base for the language-segmented public pages. Builds and stashes the SEO <see cref="PageMeta"/>.</summary>
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
        string? jsonLd = null) => new()
        {
            Culture = CurrentCulture,
            Slug = CurrentSlug,
            Title = title,
            Description = description,
            RelativePath = relativePath,
            OgType = ogType,
            OgImagePath = ogImagePath,
            Breadcrumbs = breadcrumbs ?? [],
            JsonLd = jsonLd,
        };
}
