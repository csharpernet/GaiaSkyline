using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Application.Content;

/// <summary>Read model for a <see cref="MediaAsset"/> returned by the content/media APIs.
/// <paramref name="Version"/> is the cache-busting token source: it bumps when the binary is replaced,
/// and public URLs carry it as <c>?v=</c> (Stage 8 Part B).</summary>
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
    string? Lqip = null,
    int Version = 1)
{
    private static readonly IReadOnlyDictionary<string, string> NoAlt =
        new Dictionary<string, string>();

    /// <summary>
    /// Per-language alt text (BCP-47 → text). The view resolves the request language with an English and
    /// legacy-<see cref="Alt"/> fallback; empty when the asset has no per-language alt yet.
    /// </summary>
    public IReadOnlyDictionary<string, string> AltByLang { get; init; } = NoAlt;

    /// <summary>
    /// The cache-busting query suffix — <c>?v={Version}</c> once the binary has been replaced, otherwise
    /// empty so never-replaced URLs stay byte-identical to before (Stage 8 Part B).
    /// </summary>
    public string VersionSuffix => Version <= 1 ? string.Empty : $"?v={Version}";

    /// <summary>The public URL with its cache-busting token — for OG images, JSON-LD and direct links.</summary>
    public string VersionedBlobUri => BlobUri + VersionSuffix;
}
