using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Application.Content;

/// <summary>Read model for a <see cref="MediaAsset"/> returned by the content/media APIs.</summary>
public sealed record MediaAssetDto(
    Guid Id,
    MediaKind Kind,
    string BlobUri,
    string? PosterBlobUri,
    int Width,
    int Height,
    int? DurationSec,
    long ByteSize,
    string ContentType);
