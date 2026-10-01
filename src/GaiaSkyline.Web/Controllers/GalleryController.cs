using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

public sealed class GalleryController(IContentService content) : PublicController
{
    [HttpGet("{lang:culture}/gallery")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var home = await content.GetSectionAsync("home", CurrentCulture, cancellationToken);
        var firstImage = home.Media("home.hero.poster");

        SetMeta(Meta(
            relativePath: "gallery",
            title: $"Photo gallery — {BrandName}",
            description: "Browse the apartment, the balcony view over the Douro and the Dom Luís I Bridge, and the private hot tub.",
            ogImagePath: firstImage?.BlobUri,
            breadcrumbs: [new Breadcrumb("Home", string.Empty), new Breadcrumb("Gallery", null)]));

        return View(new GalleryViewModel(home));
    }
}
