using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>EF Core reads for the admin media manager. Reads are untracked.</summary>
internal sealed class AdminMediaReadService(AppDbContext dbContext) : IAdminMediaReadService
{
    public async Task<IReadOnlyList<MediaLibraryItemDto>> GetLibraryAsync(MediaKind? kind, bool includeDeleted, CancellationToken cancellationToken)
    {
        var query = dbContext.MediaAssets.AsNoTracking().Include(a => a.AltTexts).AsQueryable();
        if (kind is { } k)
        {
            query = query.Where(a => a.Kind == k);
        }

        query = includeDeleted ? query.Where(a => a.IsDeleted) : query.Where(a => !a.IsDeleted);

        var assets = await query.OrderByDescending(a => a.UploadedAtUtc).ToListAsync(cancellationToken);
        var usage = await BuildUsageCountsAsync(cancellationToken);

        return assets.Select(a => new MediaLibraryItemDto(
            a.Id.Value,
            a.Kind,
            a.BlobUri,
            a.Lqip,
            a.Width,
            a.Height,
            a.ByteSize,
            a.UploadedAtUtc,
            a.UploadedBy,
            a.AltTexts.Select(t => t.LanguageCode).OrderBy(l => l, StringComparer.Ordinal).ToList(),
            ReadyForPublic(a),
            usage.GetValueOrDefault(a.Id.Value),
            a.IsDeleted)).ToList();
    }

    public async Task<MediaAssetDetailDto?> GetAssetAsync(Guid id, CancellationToken cancellationToken)
    {
        var assetId = MediaAssetId.From(id);
        var asset = await dbContext.MediaAssets
            .AsNoTracking()
            .Include(a => a.AltTexts)
            .FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        var usedBy = await GetUsageAsync(assetId, cancellationToken);
        var missing = asset.Kind == MediaKind.Video
            ? []
            : ContentLanguages.All.Where(l => !asset.AltTexts.Any(
                t => string.Equals(t.LanguageCode, l, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t.Text)))
                .ToList();

        return new MediaAssetDetailDto(
            asset.Id.Value,
            asset.Kind,
            asset.BlobUri,
            asset.PosterBlobUri,
            asset.Lqip,
            asset.Width,
            asset.Height,
            asset.ByteSize,
            asset.ContentType,
            asset.UploadedAtUtc,
            asset.UploadedBy,
            asset.AltTexts.ToDictionary(t => t.LanguageCode, t => t.Text, StringComparer.OrdinalIgnoreCase),
            ReadyForPublic(asset),
            missing,
            usedBy,
            asset.IsDeleted);
    }

    public async Task<bool> IsReadyForPublicAsync(Guid id, CancellationToken cancellationToken)
    {
        var assetId = MediaAssetId.From(id);
        var asset = await dbContext.MediaAssets
            .AsNoTracking()
            .Include(a => a.AltTexts)
            .FirstOrDefaultAsync(a => a.Id == assetId, cancellationToken);
        return asset is not null && ReadyForPublic(asset);
    }

    public async Task<GalleryManagerDto?> GetCollectionAsync(string key, CancellationToken cancellationToken)
    {
        var collection = await dbContext.MediaCollections
            .AsNoTracking()
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Key == key, cancellationToken);
        if (collection is null)
        {
            return null;
        }

        // All non-deleted images, with alt loaded for the readiness flag.
        var images = await dbContext.MediaAssets
            .AsNoTracking()
            .Include(a => a.AltTexts)
            .Where(a => a.Kind == MediaKind.Image && !a.IsDeleted)
            .ToListAsync(cancellationToken);
        var byId = images.ToDictionary(a => a.Id);

        var items = collection.Items
            .OrderBy(i => i.DisplayOrder)
            .Where(i => byId.ContainsKey(i.MediaAssetId))
            .Select(i => ToGalleryItem(byId[i.MediaAssetId], i.IsHero))
            .ToList();

        var inCollection = collection.Items.Select(i => i.MediaAssetId).ToHashSet();
        var available = images
            .Where(a => !inCollection.Contains(a.Id))
            .OrderByDescending(a => a.UploadedAtUtc)
            .Select(a => ToGalleryItem(a, isHero: false))
            .ToList();

        return new GalleryManagerDto(collection.Key, collection.DisplayName, items, available);
    }

    private static GalleryItemDto ToGalleryItem(MediaAsset asset, bool isHero) => new(
        asset.Id.Value,
        asset.BlobUri,
        asset.Lqip,
        asset.Width,
        asset.Height,
        isHero,
        ReadyForPublic(asset));

    // An image needs explicit alt text for every content language; a video is decorative (aria-hidden).
    private static bool ReadyForPublic(MediaAsset asset) =>
        asset.Kind == MediaKind.Video || asset.HasExplicitAltTextForAllLanguages(ContentLanguages.All);

    private async Task<IReadOnlyList<MediaUsageDto>> GetUsageAsync(MediaAssetId id, CancellationToken cancellationToken)
    {
        // Project the references (SQL-translatable) and filter in memory — comparing a converted strongly-typed
        // id inside a navigation subquery does not translate to SQL.
        var blockRefs = await dbContext.ContentBlocks
            .SelectMany(b => b.Translations, (b, t) => new { b.Key, t.ValueMediaAssetId, t.DraftMediaAssetId })
            .ToListAsync(cancellationToken);
        var blockKeys = blockRefs
            .Where(x => x.ValueMediaAssetId == id || x.DraftMediaAssetId == id)
            .Select(x => x.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var collectionRefs = await dbContext.MediaCollections
            .SelectMany(c => c.Items, (c, i) => new { c.Key, i.MediaAssetId })
            .ToListAsync(cancellationToken);
        var collectionKeys = collectionRefs
            .Where(x => x.MediaAssetId == id)
            .Select(x => x.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        return blockKeys.Select(k => new MediaUsageDto("Content block", k))
            .Concat(collectionKeys.Select(k => new MediaUsageDto("Gallery / collection", k)))
            .ToList();
    }

    private async Task<Dictionary<Guid, int>> BuildUsageCountsAsync(CancellationToken cancellationToken)
    {
        var refs = new List<Guid>();

        var published = await dbContext.ContentBlocks.SelectMany(b => b.Translations)
            .Where(t => t.ValueMediaAssetId != null).Select(t => t.ValueMediaAssetId).ToListAsync(cancellationToken);
        var drafts = await dbContext.ContentBlocks.SelectMany(b => b.Translations)
            .Where(t => t.DraftMediaAssetId != null).Select(t => t.DraftMediaAssetId).ToListAsync(cancellationToken);
        var collectionItems = await dbContext.MediaCollectionItems
            .Select(i => i.MediaAssetId).ToListAsync(cancellationToken);

        refs.AddRange(published.Where(x => x.HasValue).Select(x => x!.Value.Value));
        refs.AddRange(drafts.Where(x => x.HasValue).Select(x => x!.Value.Value));
        refs.AddRange(collectionItems.Select(x => x.Value));

        return refs.GroupBy(g => g).ToDictionary(g => g.Key, g => g.Count());
    }
}
