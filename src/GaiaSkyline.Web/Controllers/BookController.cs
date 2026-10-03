using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>The public booking entry point: date picker, guest selector and a live quote.</summary>
public sealed class BookController(IContentService content) : PublicController
{
    [HttpGet("{lang:culture}/book")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var copy = await content.GetSectionAsync("book", CurrentCulture, cancellationToken);
        var property = await content.GetPropertyAsync(cancellationToken);

        var title = copy.TextOr("book.title", "Book your stay");
        SetMeta(Meta(
            relativePath: "book",
            title: $"{title} — {BrandName}",
            description: copy.TextOr("book.subtitle", "Check availability and book the Gaia Skyline apartment direct."),
            breadcrumbs: [new Breadcrumb("Home", string.Empty), new Breadcrumb(title, null)]));

        return View(new BookPageViewModel(copy, property?.Sleeps ?? 6));
    }
}
