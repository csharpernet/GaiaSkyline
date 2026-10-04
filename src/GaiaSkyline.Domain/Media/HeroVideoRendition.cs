using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Media;

/// <summary>One transcoded output of a <see cref="HeroVideo"/> (e.g. desktop AV1). See ADR 0017.</summary>
public sealed class HeroVideoRendition : Entity<HeroVideoRenditionId>
{
    // Required by EF Core's materialization.
    private HeroVideoRendition()
    {
    }

    public HeroVideoRendition(
        HeroVideoRenditionId id,
        HeroVideoId heroVideoId,
        HeroRenditionKind kind,
        string blobUri,
        int width,
        int height,
        long byteSize)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(byteSize);

        Id = id;
        HeroVideoId = heroVideoId;
        Kind = kind;
        BlobUri = blobUri.Trim();
        Width = width;
        Height = height;
        ByteSize = byteSize;
    }

    public HeroVideoId HeroVideoId { get; private set; }

    public HeroRenditionKind Kind { get; private set; }

    public string BlobUri { get; private set; } = null!;

    public int Width { get; private set; }

    public int Height { get; private set; }

    public long ByteSize { get; private set; }

    /// <summary>The HTTP content type for the rendition's container.</summary>
    public string ContentType => Kind.ContentType();
}
