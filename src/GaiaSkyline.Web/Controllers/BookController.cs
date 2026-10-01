using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>Placeholder until the booking flow arrives in Stage 4.</summary>
public sealed class BookController : PublicController
{
    [HttpGet("{lang:culture}/book")]
    public IActionResult Index()
    {
        SetMeta(Meta(
            relativePath: "book",
            title: $"Book — {BrandName}",
            description: "Check availability and book the Gaia Skyline apartment direct. Online booking is coming soon.",
            breadcrumbs: [new Breadcrumb("Home", string.Empty), new Breadcrumb("Book", null)]));

        return View(new BookViewModel("Book your stay", "Direct online booking is coming next. In the meantime, availability is handled via the listing."));
    }

    [HttpGet("{lang:culture}/book/checkout")]
    public IActionResult Checkout()
    {
        SetMeta(Meta(
            relativePath: "book/checkout",
            title: $"Checkout — {BrandName}",
            description: "Secure checkout for your Gaia Skyline stay. Coming next.",
            breadcrumbs:
            [
                new Breadcrumb("Home", string.Empty),
                new Breadcrumb("Book", "book"),
                new Breadcrumb("Checkout", null),
            ]));

        return View("Index", new BookViewModel("Checkout", "The booking and checkout flow arrives in the next stage."));
    }
}
