namespace GaiaSkyline.Application.Content;

/// <summary>A content value for one language/kind (only the field matching the block's kind is used).</summary>
public sealed record ContentValueDto(string? Text, decimal? Number, bool? Boolean, Guid? MediaAssetId);

/// <summary>
/// Owner-only content writes. Every successful write bumps the content revision (invalidating the
/// output and content caches). Returns false when the block key doesn't exist.
/// </summary>
public interface IAdminContentService
{
    Task<bool> SetTranslationAsync(string key, string language, ContentValueDto value, string actor, CancellationToken cancellationToken);

    Task<bool> SetPublishedAsync(string key, bool published, string actor, CancellationToken cancellationToken);
}
