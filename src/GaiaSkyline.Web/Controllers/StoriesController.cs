using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

public sealed class StoriesController(IContentService content, IStorySlugRedirectResolver slugRedirects) : PublicController
{
    [HttpGet("{lang:culture}/stories")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var stories = await content.GetPublishedStoriesAsync(CurrentCulture, take: null, cancellationToken);
        var pageUrl = $"{BaseUrl}/{CurrentSlug}/stories";
        var items = stories.Select(s => ($"{BaseUrl}/{CurrentSlug}/stories/{s.Slug}", s.Title)).ToList();

        await SetMetaAsync(Meta(
            relativePath: "stories",
            title: $"Stories — {BrandName}",
            description: "Guides and notes about the apartment, Vila Nova de Gaia and the Douro — written by the host.",
            breadcrumbs: [new Breadcrumb("Home", string.Empty), new Breadcrumb("Stories", null)],
            jsonLdBlocks: [JsonLd.CollectionPage(pageUrl, $"Stories — {BrandName}", items)]), cancellationToken);

        return View(new StoriesIndexViewModel(stories));
    }

    [HttpGet("{lang:culture}/stories/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken cancellationToken)
    {
        var story = await content.GetStoryAsync(slug, CurrentCulture, cancellationToken);
        if (story is null)
        {
            // The slug may be a previous one kept after a rename, or another language's slug for the same
            // story (slugs are per language) — 301 to this language's current URL so old links survive.
            var current = await slugRedirects.ResolveCurrentSlugAsync(slug, CurrentCulture, cancellationToken);
            if (current is not null && !string.Equals(current, slug, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectPermanent($"/{CurrentSlug}/stories/{current}");
            }

            return NotFound();
        }

        // Each hreflang alternate uses that language's own slug (Stage 7 §4).
        var alternatePaths = story.SlugByLanguage.ToDictionary(
            kv => kv.Key, kv => $"stories/{kv.Value}", StringComparer.OrdinalIgnoreCase);

        var all = await content.GetPublishedStoriesAsync(CurrentCulture, take: null, cancellationToken);
        var related = all.Where(s => s.Slug != story.Slug).Take(2).ToList();
        var pageUrl = $"{BaseUrl}/{CurrentSlug}/stories/{story.Slug}";
        var coverAbsolute = story.Cover is not null ? BaseUrl + story.Cover.BlobUri : null;

        SetMeta(Meta(
            relativePath: $"stories/{story.Slug}",
            title: story.MetaTitle ?? $"{story.Title} — {BrandName}",
            description: string.IsNullOrWhiteSpace(story.MetaDescription) ? story.Excerpt : story.MetaDescription,
            ogType: "article",
            ogImagePath: story.Cover?.BlobUri,
            breadcrumbs:
            [
                new Breadcrumb("Home", string.Empty),
                new Breadcrumb("Stories", "stories"),
                new Breadcrumb(story.Title, null),
            ],
            jsonLdBlocks: [JsonLd.Article(pageUrl, story, coverAbsolute, CurrentCulture)],
            alternatePaths: alternatePaths));

        return View(new StoryDetailViewModel(story, related));
    }
}
