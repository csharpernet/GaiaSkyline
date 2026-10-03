using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>EF Core media writes for the Owner. Uploads render variants; collection edits replace items.</summary>
internal sealed class AdminMediaService(
    AppDbContext dbContext,
    IImageRenditionService renditions,
    IContentRevision revision,
    TimeProvider clock) : IAdminMediaService
{
    public async Task<Guid> UploadImageAsync(
        Stream content, string destinationDirectory, string? altText, string actor, CancellationToken cancellationToken)
    {
        var assetId = MediaAssetId.New();
        var stem = assetId.Value.ToString("N");
        var result = await renditions.GenerateAsync(content, destinationDirectory, stem, cancellationToken);

        var asset = new MediaAsset(
            assetId,
            MediaKind.Image,
            blobUri: $"/media/{stem}-1600.jpg",
            posterBlobUri: null,
            width: result.Width,
            height: result.Height,
            durationSec: null,
            byteSize: result.MasterBytes,
            contentType: "image/jpeg",
            uploadedAtUtc: clock.GetUtcNow().UtcDateTime,
            uploadedBy: actor,
            altText: altText,
            lqip: result.Lqip);

        dbContext.MediaAssets.Add(asset);
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return assetId.Value;
    }

    public async Task<bool> SetCollectionItemsAsync(
        string collectionKey, IReadOnlyList<CollectionItemDto> items, string actor, CancellationToken cancellationToken)
    {
        var collection = await dbContext.MediaCollections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Key == collectionKey, cancellationToken);
        if (collection is null)
        {
            return false;
        }

        // Replace the whole ordered list: covers reorder, add, remove and set-hero in one operation.
        dbContext.MediaCollectionItems.RemoveRange(collection.Items.ToList());
        for (var i = 0; i < items.Count; i++)
        {
            collection.AddItem(MediaCollectionItemId.New(), MediaAssetId.From(items[i].MediaAssetId), i, items[i].IsHero);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }
}
