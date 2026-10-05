using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Stories;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>Owner-only reads for the admin stories editor. Reads are untracked.</summary>
internal sealed class AdminStoryReadService(AppDbContext dbContext) : IAdminStoryReadService
{
    public async Task<IReadOnlyList<StoryListItemDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var stories = await dbContext.Stories
            .AsNoTracking()
            .Include(s => s.Translations)
            .OrderByDescending(s => s.PublishedAtUtc)
            .ToListAsync(cancellationToken);

        var covers = await CoverThumbsAsync(cancellationToken);

        return stories.Select(s => new StoryListItemDto(
            s.Id.Value,
            s.Slug,
            s.IsPublished,
            s.PublishedAtUtc,
            TitleOf(s),
            s.Translations.Count,
            ContentLanguages.All.Count(l => !HasContent(s, l)),
            covers.GetValueOrDefault(s.CoverMediaAssetId.Value))).ToList();
    }

    public async Task<StoryEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken)
    {
        var storyId = StoryId.From(id);
        var story = await dbContext.Stories
            .AsNoTracking()
            .Include(s => s.Translations)
            .Include(s => s.Aliases)
            .FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
        if (story is null)
        {
            return null;
        }

        var covers = await CoverThumbsAsync(cancellationToken);

        var translations = ContentLanguages.All.Select(lang =>
        {
            var t = story.Translations.FirstOrDefault(
                x => string.Equals(x.LanguageCode, lang, StringComparison.OrdinalIgnoreCase));
            return new StoryTranslationEditDto(
                lang,
                t?.Title ?? string.Empty,
                t?.Excerpt ?? string.Empty,
                t?.BodyRichText ?? string.Empty,
                t?.MetaTitle,
                t?.MetaDescription,
                t?.ReadingTimeMinutes ?? 0);
        }).ToList();

        return new StoryEditDto(
            story.Id.Value,
            story.Slug,
            story.IsPublished,
            story.PublishedAtUtc,
            story.CoverMediaAssetId.Value,
            covers.GetValueOrDefault(story.CoverMediaAssetId.Value),
            story.AuthorName,
            translations,
            story.Aliases.Select(a => a.OldSlug).OrderBy(s => s, StringComparer.Ordinal).ToList());
    }

    // All media blob URIs by guid (small table); the admin shows a small cover thumbnail (-400.jpg).
    private async Task<Dictionary<Guid, string>> CoverThumbsAsync(CancellationToken cancellationToken)
    {
        var media = await dbContext.MediaAssets
            .AsNoTracking()
            .Select(m => new { m.Id, m.BlobUri })
            .ToListAsync(cancellationToken);
        return media.ToDictionary(m => m.Id.Value, m => Thumb(m.BlobUri));
    }

    private static string Thumb(string blobUri) =>
        blobUri.EndsWith("-1600.jpg", StringComparison.OrdinalIgnoreCase)
            ? blobUri[..^"-1600.jpg".Length] + "-400.jpg"
            : blobUri;

    private static string TitleOf(Story story)
    {
        var en = story.Translations.FirstOrDefault(t => string.Equals(t.LanguageCode, ContentLanguages.Default, StringComparison.OrdinalIgnoreCase));
        var any = en ?? story.Translations.FirstOrDefault();
        return string.IsNullOrWhiteSpace(any?.Title) ? "(untitled)" : any!.Title;
    }

    private static bool HasContent(Story story, string lang) =>
        story.Translations.Any(t =>
            string.Equals(t.LanguageCode, lang, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t.Title));
}
