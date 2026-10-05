using System.Globalization;
using GaiaSkyline.Application.Seo;
using GaiaSkyline.Web.Localization;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;

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

    /// <summary>
    /// Like <see cref="SetMeta"/> but first applies the owner's per-page, per-language meta override (keyed by
    /// the page's <see cref="PageMeta.RelativePath"/>). A blank override field keeps the page default. Stage 7 §5.
    /// </summary>
    protected async Task SetMetaAsync(PageMeta meta, CancellationToken cancellationToken)
    {
        var resolver = HttpContext.RequestServices.GetService<IPageMetaResolver>();
        var ovr = resolver is null ? null : await resolver.ResolveAsync(meta.RelativePath, meta.Culture, cancellationToken);
        if (ovr is not null)
        {
            meta = new PageMeta
            {
                Culture = meta.Culture,
                Slug = meta.Slug,
                Title = string.IsNullOrWhiteSpace(ovr.Title) ? meta.Title : ovr.Title!,
                Description = string.IsNullOrWhiteSpace(ovr.Description) ? meta.Description : ovr.Description!,
                RelativePath = meta.RelativePath,
                OgType = meta.OgType,
                // The page's built-in noindex (checkout/confirmation) always wins; the owner can additionally
                // noindex/nofollow a normally-indexable page.
                NoIndex = meta.NoIndex || ovr.NoIndex,
                NoFollow = meta.NoFollow || ovr.NoFollow,
                OgImagePath = meta.OgImagePath,
                Breadcrumbs = meta.Breadcrumbs,
                JsonLdBlocks = meta.JsonLdBlocks,
            };
        }

        ViewData["PageMeta"] = meta;
    }

    protected PageMeta Meta(
        string relativePath,
        string title,
        string description,
        string ogType = "website",
        string? ogImagePath = null,
        IReadOnlyList<Breadcrumb>? breadcrumbs = null,
        IReadOnlyList<string>? jsonLdBlocks = null,
        bool noIndex = false) => new()
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
            NoIndex = noIndex,
        };

    /// <summary>Absolute base URL for the current request (used to build JSON-LD and OG URLs).</summary>
    protected string BaseUrl => $"{Request.Scheme}://{Request.Host}";
}
