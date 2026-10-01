using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

public sealed class StoriesController(IContentService content) : PublicController
{
    [HttpGet("{lang:culture}/stories")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var stories = await content.GetPublishedStoriesAsync(CurrentCulture, take: null, cancellationToken);

        SetMeta(Meta(
            relativePath: "stories",
            title: $"Stories — {BrandName}",
            description: "Guides and notes about the apartment, Vila Nova de Gaia and the Douro — written by the host.",
            breadcrumbs: [new Breadcrumb("Home", string.Empty), new Breadcrumb("Stories", null)]));

        return View(new StoriesIndexViewModel(stories));
    }

    [HttpGet("{lang:culture}/stories/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken cancellationToken)
    {
        var story = await content.GetStoryAsync(slug, CurrentCulture, cancellationToken);
        if (story is null)
        {
            return NotFound();
        }

        var all = await content.GetPublishedStoriesAsync(CurrentCulture, take: null, cancellationToken);
        var related = all.Where(s => s.Slug != story.Slug).Take(2).ToList();

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
            ]));

        return View(new StoryDetailViewModel(story, related));
    }
}
