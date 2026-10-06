using GaiaSkyline.Application.Content;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>
/// Maps a slug that no longer serves the requested language to the one that does (only published stories
/// redirect): a per-language or canonical alias left by a rename, or another language's slug for the same
/// story. Hit only when a <c>/{lang}/stories/{slug}</c> request finds no current story, so per-request
/// lookups are fine. Stage 7 §4.
/// </summary>
internal sealed class StorySlugRedirectResolver(AppDbContext dbContext) : IStorySlugRedirectResolver
{
    public async Task<string?> ResolveCurrentSlugAsync(string oldSlug, string language, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oldSlug))
        {
            return null;
        }

        var slug = oldSlug.Trim().ToLowerInvariant();
        var alias = await dbContext.StorySlugAliases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.OldSlug == slug && (a.LanguageCode == null || a.LanguageCode == language),
                cancellationToken);
        if (alias is not null)
        {
            // Compare the whole strongly-typed id (the converter handles equality); never project .Value into SQL.
            var storyId = alias.StoryId;
            var story = await dbContext.Stories
                .AsNoTracking()
                .Include(s => s.Translations)
                .FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
            return CurrentSlugFor(story, language, slug);
        }

        // Not an alias — maybe another language's slug for a story this language serves under its own
        // (e.g. /fr/stories/{en-slug} once FR has a French slug). 301 to this language's URL.
        var crossLanguage = await dbContext.Stories
            .AsNoTracking()
            .Include(s => s.Translations)
            .FirstOrDefaultAsync(
                s => s.IsPublished && s.Translations.Any(t => t.Slug == slug), cancellationToken);
        return CurrentSlugFor(crossLanguage, language, slug);
    }

    private static string? CurrentSlugFor(Domain.Stories.Story? story, string language, string requested)
    {
        if (story is not { IsPublished: true })
        {
            return null;
        }

        var current = story.SlugFor(language);
        return string.Equals(current, requested, StringComparison.OrdinalIgnoreCase) ? null : current;
    }
}
