using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>EF Core implementation of <see cref="IContentReadStore"/>. Reads are untracked.</summary>
internal sealed class ContentReadStore(AppDbContext dbContext) : IContentReadStore
{
    private readonly AppDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ContentBlock>> GetPublishedBlocksBySectionAsync(
        string section,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ContentBlocks
            .AsNoTracking()
            .Where(b => b.Section == section && b.IsPublished)
            .Include(b => b.Translations)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<MediaAssetId, MediaAsset>> GetMediaAssetsAsync(
        IReadOnlyCollection<MediaAssetId> ids,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new Dictionary<MediaAssetId, MediaAsset>();
        }

        var idList = ids.ToList();
        var assets = await _dbContext.MediaAssets
            .AsNoTracking()
            .Where(a => idList.Contains(a.Id))
            .ToListAsync(cancellationToken);

        return assets.ToDictionary(a => a.Id);
    }

    public async Task<MediaAsset?> GetMediaAssetAsync(MediaAssetId id, CancellationToken cancellationToken)
    {
        return await _dbContext.MediaAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }
}
