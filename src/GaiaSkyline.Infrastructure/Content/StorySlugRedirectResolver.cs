using GaiaSkyline.Application.Content;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>
/// Maps a previous story slug to the owning story's current slug (only published stories redirect). Hit only
/// when a <c>/{lang}/stories/{slug}</c> request finds no current story, so a per-request lookup is fine.
/// Stage 7 §4.
/// </summary>
internal sealed class StorySlugRedirectResolver(AppDbContext dbContext) : IStorySlugRedirectResolver
{
    public async Task<string?> ResolveCurrentSlugAsync(string oldSlug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(oldSlug))
        {
            return null;
        }

        var slug = oldSlug.Trim().ToLowerInvariant();
        var alias = await dbContext.StorySlugAliases
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.OldSlug == slug, cancellationToken);
        if (alias is null)
        {
            return null;
        }

        // Compare the whole strongly-typed id (the converter handles equality); never project .Value into SQL.
        var storyId = alias.StoryId;
        var story = await dbContext.Stories
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == storyId, cancellationToken);
        return story is { IsPublished: true } ? story.Slug : null;
    }
}
