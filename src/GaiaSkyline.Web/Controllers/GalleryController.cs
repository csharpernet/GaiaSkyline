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
        var images = await content.GetGalleryAsync("home.gallery", cancellationToken);
        var first = images.Count > 0 ? images[0].Asset : null;

        await SetMetaAsync(Meta(
            relativePath: "gallery",
            title: $"Photo gallery — {BrandName}",
            description: "Browse the apartment, the balcony view over the Douro and the Dom Luís I Bridge, and the private hot tub.",
            ogImagePath: first?.VersionedBlobUri,
            breadcrumbs: [new Breadcrumb("Home", string.Empty), new Breadcrumb("Gallery", null)]), cancellationToken);

        return View(new GalleryViewModel(images));
    }
}
