namespace GaiaSkyline.Application.Content;

/// <summary>
/// Read model for a story, resolved to one language (English fallback). <see cref="Slug"/> is that
/// language's slug; <see cref="SlugByLanguage"/> maps every content language to its own slug (languages
/// without one use the canonical slug) for hreflang alternates and the sitemap. Stage 7 §4.
/// </summary>
public sealed record StoryDto(
    string Slug,
    string Title,
    string Excerpt,
    string BodyRichText,
    string? MetaTitle,
    string? MetaDescription,
    MediaAssetDto? Cover,
    DateTime PublishedAtUtc,
    int ReadingTimeMinutes,
    string AuthorName,
    string ResolvedLanguage,
    IReadOnlyDictionary<string, string> SlugByLanguage);
