using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Domain.Stories;

namespace GaiaSkyline.Application.Content;

/// <summary>
/// Data-access port for content reads. Implemented by Infrastructure (EF Core) so EF types stay
/// out of the Application layer.
/// </summary>
public interface IContentReadStore
{
    /// <summary>Published blocks in a section, each with its translations loaded.</summary>
    Task<IReadOnlyList<ContentBlock>> GetPublishedBlocksBySectionAsync(
        string section,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<MediaAssetId, MediaAsset>> GetMediaAssetsAsync(
        IReadOnlyCollection<MediaAssetId> ids,
        CancellationToken cancellationToken);

    Task<MediaAsset?> GetMediaAssetAsync(MediaAssetId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Review>> GetPublishedReviewsAsync(CancellationToken cancellationToken);

    /// <summary>Published stories, newest first, each with its translations loaded.</summary>
    Task<IReadOnlyList<Story>> GetPublishedStoriesAsync(CancellationToken cancellationToken);

    Task<Story?> GetPublishedStoryBySlugAsync(string slug, CancellationToken cancellationToken);

    Task<Property?> GetPropertyAsync(CancellationToken cancellationToken);

    /// <summary>A media collection by key (e.g. "home.gallery") with its items loaded.</summary>
    Task<MediaCollection?> GetMediaCollectionAsync(string key, CancellationToken cancellationToken);
}
