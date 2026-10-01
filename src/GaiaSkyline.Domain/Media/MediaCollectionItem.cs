using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Media;

/// <summary>Membership of a <see cref="MediaAsset"/> in a <see cref="MediaCollection"/>.</summary>
public sealed class MediaCollectionItem : Entity<MediaCollectionItemId>
{
    // Required by EF Core's materialization.
    private MediaCollectionItem()
    {
    }

    public MediaCollectionItem(
        MediaCollectionItemId id,
        MediaCollectionId mediaCollectionId,
        MediaAssetId mediaAssetId,
        int displayOrder,
        bool isHero)
    {
        Id = id;
        MediaCollectionId = mediaCollectionId;
        MediaAssetId = mediaAssetId;
        DisplayOrder = displayOrder;
        IsHero = isHero;
    }

    public MediaCollectionId MediaCollectionId { get; private set; }

    public MediaAssetId MediaAssetId { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsHero { get; private set; }
}
