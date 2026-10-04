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
        Stream content, string destinationDirectory, string? altText, string? title, string actor, CancellationToken cancellationToken)
    {
        var assetId = MediaAssetId.New();
        var baseSlug = MediaSlug.From(title);
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = assetId.Value.ToString("N");
        }

        var stem = await EnsureUniqueStemAsync(baseSlug, excluding: null, cancellationToken);
        var result = await renditions.GenerateAsync(content, destinationDirectory, stem, cancellationToken);

        var asset = new MediaAsset(
            assetId,
            MediaKind.Image,
            blobUri: $"/media/{stem}{MediaStem.MasterSuffix}",
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

    public async Task<bool> ReplaceImageAsync(
        Guid assetId, Stream content, string destinationDirectory, string actor, CancellationToken cancellationToken)
    {
        var id = MediaAssetId.From(assetId);
        var asset = await dbContext.MediaAssets.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null || asset.Kind != MediaKind.Image)
        {
            return false;
        }

        var stem = MediaStem.Of(asset.BlobUri);
        if (stem is null)
        {
            return false; // not a pipeline raster with a known stem
        }

        // Regenerate every rendition at the same stem (overwrites the files in place).
        var result = await renditions.GenerateAsync(content, destinationDirectory, stem, cancellationToken);
        asset.ReplaceRenditions(result.Width, result.Height, result.MasterBytes, result.Lqip,
            clock.GetUtcNow().UtcDateTime, actor);

        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }

    public async Task<MediaRenameResult> RenameImageAsync(
        Guid assetId, string newTitle, string destinationDirectory, string actor, CancellationToken cancellationToken)
    {
        var id = MediaAssetId.From(assetId);
        var asset = await dbContext.MediaAssets
            .Include(a => a.Aliases)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (asset is null || asset.Kind != MediaKind.Image)
        {
            return MediaRenameResult.NotFound;
        }

        var oldStem = MediaStem.Of(asset.BlobUri);
        if (oldStem is null)
        {
            return MediaRenameResult.Unsupported;
        }

        var baseSlug = MediaSlug.From(newTitle);
        if (string.IsNullOrEmpty(baseSlug))
        {
            return MediaRenameResult.InvalidName;
        }

        var newStem = await EnsureUniqueStemAsync(baseSlug, excluding: id, cancellationToken);
        if (string.Equals(newStem, oldStem, StringComparison.Ordinal))
        {
            return MediaRenameResult.Unchanged;
        }

        MoveRenditions(destinationDirectory, oldStem, newStem);
        asset.Rename($"/media/{newStem}{MediaStem.MasterSuffix}", oldStem, newStem);

        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return MediaRenameResult.Renamed;
    }

    private static readonly int[] RenditionWidths = [400, 800, 1600];
    private static readonly string[] RenditionExtensions = ["jpg", "webp", AvifRaster.Extension];

    // Move the full raster set ({stem}-{w}.{ext}) to the new stem; skip any that are missing so a partially
    // generated or hand-seeded asset still renames cleanly.
    private static void MoveRenditions(string directory, string oldStem, string newStem)
    {
        foreach (var width in RenditionWidths)
        {
            foreach (var ext in RenditionExtensions)
            {
                var from = Path.Combine(directory, $"{oldStem}-{width}.{ext}");
                if (!File.Exists(from))
                {
                    continue;
                }

                var to = Path.Combine(directory, $"{newStem}-{width}.{ext}");
                if (File.Exists(to))
                {
                    File.Delete(to);
                }

                File.Move(from, to);
            }
        }
    }

    // A stem is free when no asset currently uses it and no alias reserves it (for a 301). Suffixes -2, -3, …
    // disambiguate. The asset being renamed (if any) is excluded so it can keep or reclaim its own stems.
    private async Task<string> EnsureUniqueStemAsync(
        string baseSlug, MediaAssetId? excluding, CancellationToken cancellationToken)
    {
        var taken = await TakenStemsAsync(excluding, cancellationToken);
        if (!taken.Contains(baseSlug))
        {
            return baseSlug;
        }

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseSlug}-{n}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private async Task<HashSet<string>> TakenStemsAsync(MediaAssetId? excluding, CancellationToken cancellationToken)
    {
        var assets = await dbContext.MediaAssets
            .AsNoTracking()
            .Select(a => new { a.Id, a.BlobUri })
            .ToListAsync(cancellationToken);
        var aliases = await dbContext.MediaAssetAliases
            .AsNoTracking()
            .Select(a => new { a.MediaAssetId, a.OldSlug })
            .ToListAsync(cancellationToken);

        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in assets)
        {
            if (excluding is { } ex && a.Id == ex)
            {
                continue;
            }

            if (MediaStem.Of(a.BlobUri) is { } stem)
            {
                taken.Add(stem);
            }
        }

        foreach (var alias in aliases)
        {
            if (excluding is { } ex && alias.MediaAssetId == ex)
            {
                continue;
            }

            taken.Add(alias.OldSlug);
        }

        return taken;
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
