namespace GaiaSkyline.Application.Content;

/// <summary>A row in the admin stories list.</summary>
public sealed record StoryListItemDto(
    Guid Id,
    string Slug,
    bool IsPublished,
    DateTime PublishedAtUtc,
    string Title,
    int LanguageCount,
    int MissingLanguageCount,
    string? CoverThumbBlobUri);

/// <summary>One language's editable fields for a story.</summary>
public sealed record StoryTranslationEditDto(
    string LanguageCode,
    string Title,
    string Excerpt,
    string BodyRichText,
    string? MetaTitle,
    string? MetaDescription,
    int ReadingTimeMinutes);

/// <summary>Full detail for editing a story: shared fields plus one entry per content language.</summary>
public sealed record StoryEditDto(
    Guid Id,
    string Slug,
    bool IsPublished,
    DateTime PublishedAtUtc,
    Guid CoverMediaAssetId,
    string? CoverThumbBlobUri,
    string AuthorName,
    IReadOnlyList<StoryTranslationEditDto> Translations,
    IReadOnlyList<string> PreviousSlugs);

/// <summary>Owner-only reads for the admin stories editor.</summary>
public interface IAdminStoryReadService
{
    Task<IReadOnlyList<StoryListItemDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>One story with every content language filled in (empty strings where a translation is missing); null if unknown.</summary>
    Task<StoryEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken);
}
