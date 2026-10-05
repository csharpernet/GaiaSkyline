using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Stories;

/// <summary>
/// A previous slug of a <see cref="Story"/>, kept after a published story is renamed so the old — possibly
/// search-indexed or linked — URL keeps working. The public story page 301-redirects an old slug to the
/// story's current slug (Stage 7 §4).
/// </summary>
public sealed class StorySlugAlias : Entity<StorySlugAliasId>
{
    // Required by EF Core's materialization.
    private StorySlugAlias()
    {
    }

    public StorySlugAlias(StorySlugAliasId id, StoryId storyId, string oldSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldSlug);

        Id = id;
        StoryId = storyId;
        OldSlug = oldSlug.Trim().ToLowerInvariant();
    }

    public StoryId StoryId { get; private set; }

    /// <summary>The vacated slug (lowercase), e.g. <c>douro-balcony-guide</c>.</summary>
    public string OldSlug { get; private set; } = null!;
}
