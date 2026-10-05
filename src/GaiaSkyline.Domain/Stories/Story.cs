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
    private readonly List<StorySlugAlias> _aliases = [];

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

    /// <summary>Previous slugs kept alive after a rename so old published URLs 301 to the current one.</summary>
    public IReadOnlyCollection<StorySlugAlias> Aliases => _aliases.AsReadOnly();

    /// <summary>
    /// Change the URL slug. When <paramref name="preservePreviousSlug"/> is true (the story is published), the
    /// vacated slug is remembered so its old URL can 301 to the new one. Reclaiming a previously-aliased slug
    /// drops that alias. No-op if the slug is unchanged.
    /// </summary>
    public void Rename(string newSlug, bool preservePreviousSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSlug);
        var normalized = newSlug.Trim().ToLowerInvariant();
        if (!IsValidSlug(normalized))
        {
            throw new ArgumentException(
                $"'{newSlug}' is not a valid URL slug (lowercase letters, digits and single hyphens).", nameof(newSlug));
        }

        if (string.Equals(normalized, Slug, StringComparison.Ordinal))
        {
            return;
        }

        var previous = Slug;
        Slug = normalized;

        if (preservePreviousSlug
            && !_aliases.Any(a => string.Equals(a.OldSlug, previous, StringComparison.OrdinalIgnoreCase)))
        {
            _aliases.Add(new StorySlugAlias(StorySlugAliasId.New(), Id, previous));
        }

        var reclaimed = _aliases.FirstOrDefault(
            a => string.Equals(a.OldSlug, normalized, StringComparison.OrdinalIgnoreCase));
        if (reclaimed is not null)
        {
            _aliases.Remove(reclaimed);
        }
    }

    public void Publish() => IsPublished = true;

    public void Unpublish() => IsPublished = false;

    public void SetPublishedDate(DateTime publishedAtUtc) => PublishedAtUtc = publishedAtUtc;

    public void SetCover(MediaAssetId coverMediaAssetId) => CoverMediaAssetId = coverMediaAssetId;

    public void SetDisplayOrder(int displayOrder) => DisplayOrder = displayOrder;

    public void SetAuthor(string authorName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        AuthorName = authorName.Trim();
    }

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
