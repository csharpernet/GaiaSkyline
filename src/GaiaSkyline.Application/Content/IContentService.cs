using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Application.Content;

/// <summary>
/// The content read API the whole site uses. Resolves a section's published blocks for a language
/// (with English fallback) and caches the result.
/// </summary>
public interface IContentService
{
    Task<ContentPayload> GetSectionAsync(string section, string language, CancellationToken cancellationToken);

    Task<MediaAssetDto?> GetMediaAssetAsync(MediaAssetId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReviewDto>> GetPublishedReviewsAsync(CancellationToken cancellationToken);
}
