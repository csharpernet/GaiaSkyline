using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Domain.Stories;
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

    public async Task<IReadOnlyList<ContentBlock>> GetAllBlocksBySectionAsync(
        string section,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ContentBlocks
            .AsNoTracking()
            .Where(b => b.Section == section)
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
            .Include(a => a.AltTexts)
            .ToListAsync(cancellationToken);

        return assets.ToDictionary(a => a.Id);
    }

    public async Task<MediaAsset?> GetMediaAssetAsync(MediaAssetId id, CancellationToken cancellationToken)
    {
        return await _dbContext.MediaAssets
            .AsNoTracking()
            .Include(a => a.AltTexts)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Review>> GetPublishedReviewsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Reviews
            .AsNoTracking()
            .Where(r => r.IsPublished)
            .OrderByDescending(r => r.StayedOn)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Story>> GetPublishedStoriesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Stories
            .AsNoTracking()
            .Where(s => s.IsPublished)
            .Include(s => s.Translations)
            .OrderByDescending(s => s.PublishedAtUtc)
            .ThenBy(s => s.DisplayOrder)
            .ToListAsync(cancellationToken);
    }

    public async Task<Story?> GetPublishedStoryBySlugAsync(string slug, string language, CancellationToken cancellationToken)
    {
        return await _dbContext.Stories
            .AsNoTracking()
            .Where(s => s.IsPublished
                && (s.Slug == slug || s.Translations.Any(t => t.LanguageCode == language && t.Slug == slug)))
            .Include(s => s.Translations)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Property?> GetPropertyAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Properties.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MediaCollection?> GetMediaCollectionAsync(string key, CancellationToken cancellationToken)
    {
        return await _dbContext.MediaCollections
            .AsNoTracking()
            .Where(c => c.Key == key)
            .Include(c => c.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
