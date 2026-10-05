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
        int readingTimeMinutes)
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
    }

    public StoryId StoryId { get; private set; }

    public string LanguageCode { get; private set; } = null!;

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
}
