using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Application.Media;

/// <summary>One place an asset is referenced (a content block or a media collection).</summary>
public sealed record MediaUsageDto(string Kind, string Reference);

/// <summary>A row in the media library grid.</summary>
public sealed record MediaLibraryItemDto(
    Guid Id,
    MediaKind Kind,
    string BlobUri,
    string? Lqip,
    int Width,
    int Height,
    long ByteSize,
    DateTime UploadedAtUtc,
    string UploadedBy,
    IReadOnlyList<string> LanguagesWithAlt,
    bool ReadyForPublic,
    int UsageCount,
    bool IsDeleted);

/// <summary>Full detail for one asset: per-language alt, the public-use readiness and where it is used.</summary>
public sealed record MediaAssetDetailDto(
    Guid Id,
    MediaKind Kind,
    string BlobUri,
    string? PosterBlobUri,
    string? Lqip,
    int Width,
    int Height,
    long ByteSize,
    string ContentType,
    DateTime UploadedAtUtc,
    string UploadedBy,
    IReadOnlyDictionary<string, string> AltByLang,
    bool ReadyForPublic,
    IReadOnlyList<string> MissingAltLanguages,
    IReadOnlyList<MediaUsageDto> UsedBy,
    bool IsDeleted,
    string? Slug = null,
    IReadOnlyList<string>? PreviousSlugs = null);

/// <summary>One image in the gallery manager: its order comes from position; hero and readiness are flags.</summary>
public sealed record GalleryItemDto(
    Guid MediaAssetId,
    string BlobUri,
    string? Lqip,
    int Width,
    int Height,
    bool IsHero,
    bool ReadyForPublic);

/// <summary>A media collection for the gallery manager: its ordered items plus the images available to add.</summary>
public sealed record GalleryManagerDto(
    string Key,
    string DisplayName,
    IReadOnlyList<GalleryItemDto> Items,
    IReadOnlyList<GalleryItemDto> Available);

/// <summary>
/// Owner-only media reads for the admin manager: the library grid, one asset's detail with per-language alt
/// and "where used", the public-use readiness gate (an image needs alt text in every content language before
/// it may be placed on a public page), and the gallery collection for reorder/hero/add-remove.
/// </summary>
public interface IAdminMediaReadService
{
    Task<IReadOnlyList<MediaLibraryItemDto>> GetLibraryAsync(MediaKind? kind, bool includeDeleted, CancellationToken cancellationToken);

    Task<MediaAssetDetailDto?> GetAssetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>True when the image has explicit alt text for every content language (videos are decorative → always true).</summary>
    Task<bool> IsReadyForPublicAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>A media collection (e.g. "home.gallery") with its ordered items and the images available to add; null if unknown.</summary>
    Task<GalleryManagerDto?> GetCollectionAsync(string key, CancellationToken cancellationToken);
}
