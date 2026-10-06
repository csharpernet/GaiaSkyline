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

    public StorySlugAlias(StorySlugAliasId id, StoryId storyId, string oldSlug, string? languageCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldSlug);

        Id = id;
        StoryId = storyId;
        OldSlug = oldSlug.Trim().ToLowerInvariant();
        LanguageCode = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.Trim();
    }

    public StoryId StoryId { get; private set; }

    /// <summary>The vacated slug (lowercase), e.g. <c>douro-balcony-guide</c>.</summary>
    public string OldSlug { get; private set; } = null!;

    /// <summary>
    /// The language whose slug was vacated, or null when the canonical slug was renamed (it served every
    /// language without a slug of its own, so the alias matches any language). Stage 7 §4.
    /// </summary>
    public string? LanguageCode { get; private set; }
}
