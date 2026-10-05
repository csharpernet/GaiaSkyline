using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Stories;
using GaiaSkyline.Infrastructure.Media;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>
/// Owner-only story writes: create/update (slug auto-suggested from the English title and kept unique, body
/// sanitised, reading time computed on save), publish/unpublish and delete. Renaming a published story's slug
/// records an alias so the old URL 301s to the new one (Stage 7 §4). Each write bumps the content revision.
/// </summary>
internal sealed class AdminStoryService(
    AppDbContext dbContext,
    IHtmlContentSanitizer sanitizer,
    IContentRevision revision) : IAdminStoryService
{
    public async Task<StorySaveResult> CreateAsync(StoryWriteModel model, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        var englishTitle = TitleFor(model, ContentLanguages.Default);
        if (string.IsNullOrWhiteSpace(englishTitle))
        {
            return StorySaveResult.Fail("Give the story an English title first.");
        }

        if (!await CoverExistsAsync(model.CoverMediaAssetId, cancellationToken))
        {
            return StorySaveResult.Fail("Choose a cover image.");
        }

        string slug;
        try
        {
            slug = await ResolveSlugAsync(model.Slug, englishTitle, excluding: null, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return StorySaveResult.Fail(ex.Message);
        }

        var id = StoryId.New();
        var story = new Story(
            id, slug, MediaAssetId.From(model.CoverMediaAssetId), model.PublishedAtUtc,
            model.IsPublished, displayOrder: 0, authorName: model.AuthorName);
        ApplyTranslations(story, model);

        dbContext.Stories.Add(story);
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return StorySaveResult.Success(id.Value);
    }

    public async Task<StorySaveResult> UpdateAsync(Guid id, StoryWriteModel model, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);

        var storyId = StoryId.From(id);
        var story = await dbContext.Stories
            .Include(s => s.Translations)
            .Include(s => s.Aliases)
            .FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
        if (story is null)
        {
            return StorySaveResult.Fail("Story not found.");
        }

        var englishTitle = TitleFor(model, ContentLanguages.Default);
        if (string.IsNullOrWhiteSpace(englishTitle))
        {
            return StorySaveResult.Fail("Give the story an English title first.");
        }

        if (!await CoverExistsAsync(model.CoverMediaAssetId, cancellationToken))
        {
            return StorySaveResult.Fail("Choose a cover image.");
        }

        string slug;
        try
        {
            slug = await ResolveSlugAsync(model.Slug, englishTitle, excluding: storyId, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return StorySaveResult.Fail(ex.Message);
        }

        // A published story's old slug must keep working; a draft has no public URL to preserve.
        var wasPublished = story.IsPublished;
        story.Rename(slug, preservePreviousSlug: wasPublished);
        story.SetCover(MediaAssetId.From(model.CoverMediaAssetId));
        story.SetAuthor(model.AuthorName);
        story.SetPublishedDate(model.PublishedAtUtc);
        if (model.IsPublished)
        {
            story.Publish();
        }
        else
        {
            story.Unpublish();
        }

        ApplyTranslations(story, model);

        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return StorySaveResult.Success(storyId.Value);
    }

    public async Task<bool> DeleteAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var storyId = StoryId.From(id);
        var story = await dbContext.Stories.FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
        if (story is null)
        {
            return false;
        }

        dbContext.Stories.Remove(story); // translations + aliases cascade
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }

    private void ApplyTranslations(Story story, StoryWriteModel model)
    {
        foreach (var input in model.Translations)
        {
            if (string.IsNullOrWhiteSpace(input.Title))
            {
                continue; // a language with no title is not authored yet
            }

            var body = sanitizer.Sanitize(input.BodyRichText);
            story.SetTranslation(
                input.LanguageCode,
                input.Title.Trim(),
                (input.Excerpt ?? string.Empty).Trim(),
                body,
                string.IsNullOrWhiteSpace(input.MetaTitle) ? null : input.MetaTitle.Trim(),
                string.IsNullOrWhiteSpace(input.MetaDescription) ? null : input.MetaDescription.Trim(),
                EstimateReadingMinutes(body));
        }
    }

    private static string? TitleFor(StoryWriteModel model, string language) =>
        model.Translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase))?.Title;

    private async Task<bool> CoverExistsAsync(Guid coverId, CancellationToken cancellationToken)
    {
        if (coverId == Guid.Empty)
        {
            return false;
        }

        var id = MediaAssetId.From(coverId);
        return await dbContext.MediaAssets.AnyAsync(m => m.Id == id, cancellationToken);
    }

    // Use the supplied slug if valid, else derive one from the English title; then make it unique across
    // current slugs and aliases (append -2, -3, …). Throws ArgumentException for an unusable supplied slug.
    private async Task<string> ResolveSlugAsync(string? supplied, string englishTitle, StoryId? excluding, CancellationToken cancellationToken)
    {
        var baseSlug = string.IsNullOrWhiteSpace(supplied) ? MediaSlug.From(englishTitle) : MediaSlug.From(supplied);
        if (string.IsNullOrEmpty(baseSlug))
        {
            throw new ArgumentException("That title/slug has no usable URL characters — add some letters or numbers.");
        }

        var taken = await TakenSlugsAsync(excluding, cancellationToken);
        if (!taken.Contains(baseSlug))
        {
            return baseSlug;
        }

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseSlug}-{n}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private async Task<HashSet<string>> TakenSlugsAsync(StoryId? excluding, CancellationToken cancellationToken)
    {
        var slugs = await dbContext.Stories
            .AsNoTracking()
            .Select(s => new { s.Id, s.Slug })
            .ToListAsync(cancellationToken);
        var aliases = await dbContext.StorySlugAliases
            .AsNoTracking()
            .Select(a => new { a.StoryId, a.OldSlug })
            .ToListAsync(cancellationToken);

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in slugs)
        {
            if (excluding is { } ex && s.Id == ex)
            {
                continue;
            }

            taken.Add(s.Slug);
        }

        foreach (var a in aliases)
        {
            if (excluding is { } ex && a.StoryId == ex)
            {
                continue;
            }

            taken.Add(a.OldSlug);
        }

        return taken;
    }

    // ~200 words per minute, at least one minute.
    private static int EstimateReadingMinutes(string html)
    {
        var words = html.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(words / 200.0));
    }
}
