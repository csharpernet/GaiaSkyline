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

    public async Task<bool> SetAltTextsAsync(
        Guid assetId, IReadOnlyDictionary<string, string?> altByLanguage, string actor, CancellationToken cancellationToken)
    {
        var id = MediaAssetId.From(assetId);
        var asset = await dbContext.MediaAssets
            .Include(a => a.AltTexts)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return false;
        }

        foreach (var (language, text) in altByLanguage)
        {
            asset.SetAltText(language, text);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }

    private const string MasterSuffix = "-1600.jpg";

    public async Task<bool> ReplaceImageAsync(
        Guid assetId, Stream content, string destinationDirectory, string actor, CancellationToken cancellationToken)
    {
        var id = MediaAssetId.From(assetId);
        var asset = await dbContext.MediaAssets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null || asset.Kind != MediaKind.Image)
        {
            return false;
        }

        var fileName = Path.GetFileName(asset.BlobUri);
        if (!fileName.EndsWith(MasterSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false; // not a pipeline raster with a known stem
        }

        var stem = fileName[..^MasterSuffix.Length];
        // Regenerate every rendition at the same stem (overwrites the files in place).
        var result = await renditions.GenerateAsync(content, destinationDirectory, stem, cancellationToken);
        asset.ReplaceRenditions(result.Width, result.Height, result.MasterBytes, result.Lqip,
            clock.GetUtcNow().UtcDateTime, actor);

        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }

    public async Task<MediaDeleteResult> SoftDeleteAsync(Guid assetId, string actor, CancellationToken cancellationToken)
    {
        var id = MediaAssetId.From(assetId);
        var asset = await dbContext.MediaAssets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return MediaDeleteResult.NotFound;
        }

        if (await IsInUseAsync(id, cancellationToken))
        {
            return MediaDeleteResult.InUse;
        }

        asset.SoftDelete(clock.GetUtcNow().UtcDateTime);
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return MediaDeleteResult.Deleted;
    }

    public async Task<bool> RestoreAsync(Guid assetId, string actor, CancellationToken cancellationToken)
    {
        var id = MediaAssetId.From(assetId);
        var asset = await dbContext.MediaAssets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null)
        {
            return false;
        }

        asset.Restore();
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }

    // Referenced by any content block (published or draft) or any collection item. Project then filter in
    // memory — a converted strongly-typed id does not translate inside a navigation subquery.
    private async Task<bool> IsInUseAsync(MediaAssetId id, CancellationToken cancellationToken)
    {
        var blockRefs = await dbContext.ContentBlocks
            .SelectMany(b => b.Translations, (b, t) => new { t.ValueMediaAssetId, t.DraftMediaAssetId })
            .ToListAsync(cancellationToken);
        if (blockRefs.Any(x => x.ValueMediaAssetId == id || x.DraftMediaAssetId == id))
        {
            return true;
        }

        var collectionRefs = await dbContext.MediaCollectionItems
            .Select(i => i.MediaAssetId).ToListAsync(cancellationToken);
        return collectionRefs.Any(x => x == id);
    }
}
