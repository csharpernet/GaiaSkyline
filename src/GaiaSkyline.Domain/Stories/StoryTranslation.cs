using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Stories;

/// <summary>A single language's content for a <see cref="Story"/>.</summary>
public sealed class StoryTranslation : Entity<StoryTranslationId>
{
    // Required by EF Core's materialization.
    private StoryTranslation()
    {
    }

    public StoryTranslation(
        StoryTranslationId id,
        StoryId storyId,
        string languageCode,
        string title,
        string excerpt,
        string bodyRichText,
        string? metaTitle,
        string? metaDescription,
        int readingTimeMinutes,
        string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegative(readingTimeMinutes);

        Id = id;
        StoryId = storyId;
        LanguageCode = languageCode.Trim();
        Title = title.Trim();
        // Excerpt and body are optional (a title-only draft is valid); they are filled in as the story is written.
        Excerpt = (excerpt ?? string.Empty).Trim();
        BodyRichText = bodyRichText ?? string.Empty;
        MetaTitle = string.IsNullOrWhiteSpace(metaTitle) ? null : metaTitle.Trim();
        MetaDescription = string.IsNullOrWhiteSpace(metaDescription) ? null : metaDescription.Trim();
        ReadingTimeMinutes = readingTimeMinutes;
        SetSlug(slug);
    }

    public StoryId StoryId { get; private set; }

    public string LanguageCode { get; private set; } = null!;

    /// <summary>
    /// This language's URL slug, unique per language (Stage 7 §4). New translations start on the story's
    /// canonical slug and follow it until the owner gives the language its own slug.
    /// </summary>
    public string Slug { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string Excerpt { get; private set; } = null!;

    public string BodyRichText { get; private set; } = null!;

    public string? MetaTitle { get; private set; }

    public string? MetaDescription { get; private set; }

    public int ReadingTimeMinutes { get; private set; }

    internal void Update(
        string title,
        string excerpt,
        string bodyRichText,
        string? metaTitle,
        string? metaDescription,
        int readingTimeMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentOutOfRangeException.ThrowIfNegative(readingTimeMinutes);

        Title = title.Trim();
        Excerpt = (excerpt ?? string.Empty).Trim();
        BodyRichText = bodyRichText ?? string.Empty;
        MetaTitle = string.IsNullOrWhiteSpace(metaTitle) ? null : metaTitle.Trim();
        MetaDescription = string.IsNullOrWhiteSpace(metaDescription) ? null : metaDescription.Trim();
        ReadingTimeMinutes = readingTimeMinutes;
    }

    internal void SetSlug(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        var normalized = slug.Trim().ToLowerInvariant();
        if (!Story.IsValidSlug(normalized))
        {
            throw new ArgumentException(
                $"'{slug}' is not a valid URL slug (lowercase letters, digits and single hyphens).", nameof(slug));
        }

        Slug = normalized;
    }
}
