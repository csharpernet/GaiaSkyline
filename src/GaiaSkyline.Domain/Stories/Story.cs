using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Stories;

/// <summary>
/// A long-form editorial story (blog entry). Owned, multilingual content with per-language
/// <see cref="StoryTranslation"/>s and a shared URL <see cref="Slug"/>.
/// </summary>
public sealed class Story : Entity<StoryId>
{
    public const string SectionName = "stories";

    private readonly List<StoryTranslation> _translations = [];

    // Required by EF Core's materialization.
    private Story()
    {
    }

    public Story(
        StoryId id,
        string slug,
        MediaAssetId coverMediaAssetId,
        DateTime publishedAtUtc,
        bool isPublished,
        int displayOrder,
        string authorName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);

        var normalizedSlug = slug.Trim().ToLowerInvariant();
        if (!IsValidSlug(normalizedSlug))
        {
            throw new ArgumentException(
                $"'{slug}' is not a valid URL slug (lowercase letters, digits and single hyphens).", nameof(slug));
        }

        Id = id;
        Slug = normalizedSlug;
        CoverMediaAssetId = coverMediaAssetId;
        PublishedAtUtc = publishedAtUtc;
        IsPublished = isPublished;
        DisplayOrder = displayOrder;
        AuthorName = authorName.Trim();
    }

    /// <summary>URL slug, unique across stories (shared across languages).</summary>
    public string Slug { get; private set; } = null!;

    public MediaAssetId CoverMediaAssetId { get; private set; }

    public DateTime PublishedAtUtc { get; private set; }

    public bool IsPublished { get; private set; }

    public int DisplayOrder { get; private set; }

    /// <summary>Byline; defaults to the property name at seed/author time.</summary>
    public string AuthorName { get; private set; } = null!;

    public IReadOnlyCollection<StoryTranslation> Translations => _translations.AsReadOnly();

    /// <summary>Insert or update the translation for a language (upsert by language code).</summary>
    public StoryTranslation SetTranslation(
        string languageCode,
        string title,
        string excerpt,
        string bodyRichText,
        string? metaTitle,
        string? metaDescription,
        int readingTimeMinutes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        var normalized = languageCode.Trim();

        var existing = _translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Update(title, excerpt, bodyRichText, metaTitle, metaDescription, readingTimeMinutes);
            return existing;
        }

        var translation = new StoryTranslation(
            StoryTranslationId.New(), Id, normalized, title, excerpt, bodyRichText,
            metaTitle, metaDescription, readingTimeMinutes);
        _translations.Add(translation);
        return translation;
    }

    private static bool IsValidSlug(string slug)
    {
        if (slug.Length == 0 || slug[0] == '-' || slug[^1] == '-')
        {
            return false;
        }

        var previousHyphen = false;
        foreach (var c in slug)
        {
            if (c == '-')
            {
                if (previousHyphen)
                {
                    return false;
                }

                previousHyphen = true;
                continue;
            }

            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9')))
            {
                return false;
            }

            previousHyphen = false;
        }

        return true;
    }
}
