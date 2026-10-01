using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Media;

/// <summary>A named, ordered set of media (e.g. "home.gallery").</summary>
public sealed class MediaCollection : Entity<MediaCollectionId>
{
    private readonly List<MediaCollectionItem> _items = [];

    // Required by EF Core's materialization.
    private MediaCollection()
    {
    }

    public MediaCollection(MediaCollectionId id, string key, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Id = id;
        Key = key.Trim();
        DisplayName = displayName.Trim();
    }

    /// <summary>Stable key, unique across collections (e.g. "home.gallery").</summary>
    public string Key { get; private set; } = null!;

    public string DisplayName { get; private set; } = null!;

    public IReadOnlyCollection<MediaCollectionItem> Items => _items.AsReadOnly();

    public MediaCollectionItem AddItem(
        MediaCollectionItemId id,
        MediaAssetId mediaAssetId,
        int displayOrder,
        bool isHero)
    {
        var item = new MediaCollectionItem(id, Id, mediaAssetId, displayOrder, isHero);
        _items.Add(item);
        return item;
    }
}
