using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner stories editor at /admin/stories: list, create, edit (per-language tabs + TipTap body, cover
/// picker, draft/publish, auto slug + reading time) and delete. Renaming a published story's slug records a
/// 301 (Stage 7 §4). Reuses the content editor's JS glue (tabs, rich-text, media picker).
/// </summary>
[Route("admin/stories")]
public sealed class StoriesAdminController(
    IAdminStoryReadService read,
    IAdminStoryService stories,
    IAdminMediaReadService media,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Stories";
        var items = await read.GetAllAsync(cancellationToken);
        return View(items);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "New story";
        var empty = ContentLanguages.All
            .Select(l => new StoryTranslationEditDto(l, string.Empty, string.Empty, string.Empty, null, null, 0, string.Empty))
            .ToList();
        var model = new StoryEditViewModel(
            null, null, string.Empty, Guid.Empty, DateTime.UtcNow.Date, false,
            empty, [], await ImagesAsync(cancellationToken));
        return View("Edit", model);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var story = await read.GetForEditAsync(id, cancellationToken);
        if (story is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Edit story";
        var model = new StoryEditViewModel(
            story.Id, story.Slug, story.AuthorName, story.CoverMediaAssetId, story.PublishedAtUtc.Date,
            story.IsPublished, story.Translations, story.PreviousSlugs, await ImagesAsync(cancellationToken));
        return View(model);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StoryForm form, CancellationToken cancellationToken)
    {
        var result = await stories.CreateAsync(ToWriteModel(form), ActorName, cancellationToken);
        if (!result.Ok)
        {
            Toast(result.Error ?? "Could not save the story.", "error");
            return await RedisplayAsync(null, form, cancellationToken);
        }

        await audit.WriteAsync("story.create", ActorId, Ip, "Story", result.StoryId!.ToString(), null, cancellationToken);
        Toast("Story created.");
        return LocalRedirect($"/admin/stories/{result.StoryId}");
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, StoryForm form, CancellationToken cancellationToken)
    {
        var result = await stories.UpdateAsync(id, ToWriteModel(form), ActorName, cancellationToken);
        if (!result.Ok)
        {
            Toast(result.Error ?? "Could not save the story.", "error");
            return await RedisplayAsync(id, form, cancellationToken);
        }

        await audit.WriteAsync("story.update", ActorId, Ip, "Story", id.ToString(), null, cancellationToken);
        Toast("Story saved.");
        return LocalRedirect($"/admin/stories/{id}");
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await stories.DeleteAsync(id, ActorName, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("story.delete", ActorId, Ip, "Story", id.ToString(), null, cancellationToken);
        Toast("Story deleted.");
        return LocalRedirect("/admin/stories");
    }

    private static StoryWriteModel ToWriteModel(StoryForm form) => new(
        form.Slug,
        string.IsNullOrWhiteSpace(form.AuthorName) ? "GaiaSkyline" : form.AuthorName.Trim(),
        form.CoverMediaAssetId,
        DateTime.SpecifyKind(form.PublishedDate, DateTimeKind.Utc),
        form.IsPublished,
        form.Translations.Select(t => new StoryTranslationInput(
            t.LanguageCode, t.Title, t.Excerpt, t.Body, t.MetaTitle, t.MetaDescription, t.Slug)).ToList());

    // Re-render the editor preserving what the owner typed when a save is rejected.
    private async Task<IActionResult> RedisplayAsync(Guid? id, StoryForm form, CancellationToken cancellationToken)
    {
        var translations = ContentLanguages.All.Select(l =>
        {
            var t = form.Translations.FirstOrDefault(x => string.Equals(x.LanguageCode, l, StringComparison.OrdinalIgnoreCase));
            return new StoryTranslationEditDto(l, t?.Title ?? string.Empty, t?.Excerpt ?? string.Empty,
                t?.Body ?? string.Empty, t?.MetaTitle, t?.MetaDescription, 0, t?.Slug ?? string.Empty);
        }).ToList();
        var model = new StoryEditViewModel(
            id, form.Slug, form.AuthorName, form.CoverMediaAssetId, form.PublishedDate, form.IsPublished,
            translations, [], await ImagesAsync(cancellationToken));
        return View("Edit", model);
    }

    private async Task<IReadOnlyList<MediaLibraryItemDto>> ImagesAsync(CancellationToken cancellationToken) =>
        await media.GetLibraryAsync(MediaKind.Image, includeDeleted: false, cancellationToken);
}
