using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Media;

/// <summary>
/// An image or video. <see cref="BlobUri"/> is a relative path under wwwroot/media in dev and an
/// Azure Blob URL in production (swapped behind <c>IMediaStorage</c>; see ADR 0006).
/// </summary>
public sealed class MediaAsset : Entity<MediaAssetId>
{
    // Required by EF Core's materialization.
    private MediaAsset()
    {
    }

    public MediaAsset(
        MediaAssetId id,
        MediaKind kind,
        string blobUri,
        string? posterBlobUri,
        int width,
        int height,
        int? durationSec,
        long byteSize,
        string contentType,
        DateTime uploadedAtUtc,
        string uploadedBy,
        string? altText = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(byteSize);

        Id = id;
        Kind = kind;
        BlobUri = blobUri.Trim();
        PosterBlobUri = string.IsNullOrWhiteSpace(posterBlobUri) ? null : posterBlobUri.Trim();
        Width = width;
        Height = height;
        DurationSec = durationSec;
        ByteSize = byteSize;
        ContentType = contentType.Trim();
        UploadedAtUtc = uploadedAtUtc;
        UploadedBy = uploadedBy;
        AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
    }

    public MediaKind Kind { get; private set; }

    public string BlobUri { get; private set; } = null!;

    public string? PosterBlobUri { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int? DurationSec { get; private set; }

    public long ByteSize { get; private set; }

    public string ContentType { get; private set; } = null!;

    public DateTime UploadedAtUtc { get; private set; }

    public string UploadedBy { get; private set; } = null!;

    /// <summary>Descriptive alternative text for accessibility and image SEO (required in Stage 7).</summary>
    public string? AltText { get; private set; }
}

