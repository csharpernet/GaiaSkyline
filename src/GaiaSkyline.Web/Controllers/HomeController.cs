using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
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
        var gallery = await content.GetGalleryAsync("home.gallery", cancellationToken);
        var property = await content.GetPropertyAsync(cancellationToken);

        var headline = home.TextOr("home.hero.headline", "Wake up to the Dom Luís I Bridge");
        var subheadline = home.TextOr(
            "home.hero.subheadline",
            "A two-bedroom apartment above the Douro in Vila Nova de Gaia, with the only true hot tub in the building.");
        var poster = home.Media("home.hero.poster");
        var posterAbsolute = poster is not null ? BaseUrl + poster.BlobUri : null;

        var jsonLd = new List<string>();
        if (property is not null)
        {
            var amenityList = amenities.Items.Values
                .Where(v => !string.IsNullOrWhiteSpace(v.Text))
                .Select(v => (v.Text!, v.Boolean ?? true))
                .ToList();
            jsonLd.Add(JsonLd.LodgingBusiness(
                $"{BaseUrl}/{CurrentSlug}",
                property,
                posterAbsolute,
                home.Number("home.reviews.aggregate.value"),
                home.Number("home.reviews.aggregate.count"),
                amenityList,
                reviews));
        }

        // FAQPage only for genuinely answered FAQs (placeholders like "TBD" are excluded, not rendered empty).
        var faqEntries = new List<(string, string)>();
        for (var i = 1; i <= 6; i++)
        {
            var question = faq.Text($"faq.{i}.q");
            var answer = faq.Text($"faq.{i}.a");
            if (!string.IsNullOrWhiteSpace(question) && !string.IsNullOrWhiteSpace(answer)
                && !answer.Contains("TBD", StringComparison.OrdinalIgnoreCase) && !answer.StartsWith('‹'))
            {
                faqEntries.Add((question, answer));
            }
        }

        if (faqEntries.Count > 0)
        {
            jsonLd.Add(JsonLd.FaqPage(faqEntries));
        }

        SetMeta(Meta(
            relativePath: string.Empty,
            title: $"{BrandName} — {headline}",
            description: subheadline,
            ogType: "website",
            ogImagePath: poster?.BlobUri,
            jsonLdBlocks: jsonLd));

        return View(new HomeViewModel(home, amenities, rules, faq, reviews, stories, gallery, property));
    }
}
