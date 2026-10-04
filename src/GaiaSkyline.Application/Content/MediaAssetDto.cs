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
    string ContentType,
    string? Alt,
    string? Lqip = null)
{
    private static readonly IReadOnlyDictionary<string, string> NoAlt =
        new Dictionary<string, string>();

    /// <summary>
    /// Per-language alt text (BCP-47 → text). The view resolves the request language with an English and
    /// legacy-<see cref="Alt"/> fallback; empty when the asset has no per-language alt yet.
    /// </summary>
    public IReadOnlyDictionary<string, string> AltByLang { get; init; } = NoAlt;
}
