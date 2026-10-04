using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Domain.Stories;

namespace GaiaSkyline.Application.Tests.Content;

/// <summary>In-memory <see cref="IContentReadStore"/> for unit tests; counts section queries.</summary>
internal sealed class FakeContentReadStore : IContentReadStore
{
    public List<ContentBlock> Blocks { get; } = [];

    public Dictionary<MediaAssetId, MediaAsset> Media { get; } = [];

    public int SectionQueryCount { get; private set; }

    public Task<IReadOnlyList<ContentBlock>> GetPublishedBlocksBySectionAsync(
        string section,
        CancellationToken cancellationToken)
    {
        SectionQueryCount++;
        IReadOnlyList<ContentBlock> result = Blocks
            .Where(b => b.Section == section && b.IsPublished)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ContentBlock>> GetAllBlocksBySectionAsync(
        string section,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ContentBlock> result = Blocks.Where(b => b.Section == section).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<MediaAssetId, MediaAsset>> GetMediaAssetsAsync(
        IReadOnlyCollection<MediaAssetId> ids,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<MediaAssetId, MediaAsset> result = Media
            .Where(kv => ids.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return Task.FromResult(result);
    }

    public Task<MediaAsset?> GetMediaAssetAsync(MediaAssetId id, CancellationToken cancellationToken) =>
        Task.FromResult(Media.TryGetValue(id, out var asset) ? asset : null);

    public List<Review> Reviews { get; } = [];

    public Task<IReadOnlyList<Review>> GetPublishedReviewsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Review> result = Reviews.Where(r => r.IsPublished).ToList();
        return Task.FromResult(result);
    }

    public List<Story> Stories { get; } = [];

    public Task<IReadOnlyList<Story>> GetPublishedStoriesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Story> result = Stories
            .Where(s => s.IsPublished)
            .OrderByDescending(s => s.PublishedAtUtc)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<Story?> GetPublishedStoryBySlugAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Stories.FirstOrDefault(s => s.IsPublished && s.Slug == slug));

    public Property? Property { get; set; }

    public List<MediaCollection> Collections { get; } = [];

    public Task<Property?> GetPropertyAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Property);

    public Task<MediaCollection?> GetMediaCollectionAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(Collections.FirstOrDefault(c => c.Key == key));
}
