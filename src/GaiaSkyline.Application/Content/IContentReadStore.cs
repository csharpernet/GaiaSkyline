using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Reviews;

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
}
