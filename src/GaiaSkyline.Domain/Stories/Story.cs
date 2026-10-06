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

    /// <summary>
    /// The canonical URL slug, unique across stories. Languages without their own
    /// <see cref="StoryTranslation.Slug"/> publish under this one (Stage 7 §4).
    /// </summary>
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

        // Translations that never got their own slug follow the canonical one.
        foreach (var t in _translations.Where(t => string.Equals(t.Slug, previous, StringComparison.OrdinalIgnoreCase)))
        {
            t.SetSlug(normalized);
        }

        if (preservePreviousSlug
            && !_aliases.Any(a => a.LanguageCode is null && string.Equals(a.OldSlug, previous, StringComparison.OrdinalIgnoreCase)))
        {
            // A null language: the canonical slug served every language without its own slug.
            _aliases.Add(new StorySlugAlias(StorySlugAliasId.New(), Id, previous, languageCode: null));
        }

        _aliases.RemoveAll(a => string.Equals(a.OldSlug, normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>This language's public slug: its translation's own slug, or the canonical one.</summary>
    public string SlugFor(string languageCode)
    {
        var translation = _translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(translation?.Slug) ? Slug : translation.Slug;
    }

    /// <summary>
    /// Give one language its own slug (Stage 7 §4: slugs are unique per language). When
    /// <paramref name="preservePreviousSlug"/> is true and the vacated slug was not the canonical one (which
    /// keeps resolving by itself), it is remembered per-language so the old URL can 301. No-op when unchanged.
    /// </summary>
    public void SetTranslationSlug(string languageCode, string newSlug, bool preservePreviousSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(newSlug);

        var translation = _translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, languageCode.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The story has no '{languageCode}' translation to slug.");

        var normalized = newSlug.Trim().ToLowerInvariant();
        if (!IsValidSlug(normalized))
        {
            throw new ArgumentException(
                $"'{newSlug}' is not a valid URL slug (lowercase letters, digits and single hyphens).", nameof(newSlug));
        }

        if (string.Equals(normalized, translation.Slug, StringComparison.Ordinal))
        {
            return;
        }

        var previous = translation.Slug;
        translation.SetSlug(normalized);

        if (preservePreviousSlug
            && !string.Equals(previous, Slug, StringComparison.OrdinalIgnoreCase)
            && !_aliases.Any(a => string.Equals(a.OldSlug, previous, StringComparison.OrdinalIgnoreCase)))
        {
            _aliases.Add(new StorySlugAlias(StorySlugAliasId.New(), Id, previous, translation.LanguageCode));
        }

        _aliases.RemoveAll(a =>
            string.Equals(a.OldSlug, normalized, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.LanguageCode, translation.LanguageCode, StringComparison.OrdinalIgnoreCase));
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

        // A new translation publishes under the canonical slug until it is given its own (Stage 7 §4).
        var translation = new StoryTranslation(
            StoryTranslationId.New(), Id, normalized, title, excerpt, bodyRichText,
            metaTitle, metaDescription, readingTimeMinutes, Slug);
        _translations.Add(translation);
        return translation;
    }

    internal static bool IsValidSlug(string slug)
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
