namespace GaiaSkyline.Application.Content;

/// <summary>Read model for a story, resolved to one language (English fallback).</summary>
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
    string ResolvedLanguage);
