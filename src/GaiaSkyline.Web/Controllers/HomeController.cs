using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

public sealed class HomeController(IContentService content) : PublicController
{
    [HttpGet("{lang:culture}")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var home = await content.GetSectionAsync("home", CurrentCulture, cancellationToken);
        var amenities = await content.GetSectionAsync("amenities", CurrentCulture, cancellationToken);
        var rules = await content.GetSectionAsync("rules", CurrentCulture, cancellationToken);
        var faq = await content.GetSectionAsync("faq", CurrentCulture, cancellationToken);
        var reviews = await content.GetPublishedReviewsAsync(cancellationToken);
        var stories = await content.GetPublishedStoriesAsync(CurrentCulture, 3, cancellationToken);

        var headline = home.TextOr("home.hero.headline", "Wake up to the Dom Luís I Bridge");
        var subheadline = home.TextOr(
            "home.hero.subheadline",
            "A two-bedroom apartment above the Douro in Vila Nova de Gaia, with the only true hot tub in the building.");
        var poster = home.Media("home.hero.poster");

        SetMeta(Meta(
            relativePath: string.Empty,
            title: $"{BrandName} — {headline}",
            description: subheadline,
            ogType: "website",
            ogImagePath: poster?.BlobUri));

        return View(new HomeViewModel(home, amenities, rules, faq, reviews, stories));
    }
}
