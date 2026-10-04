namespace GaiaSkyline.Application.Content;

/// <summary>A content value for one language/kind (only the field matching the block's kind is used).</summary>
public sealed record ContentValueDto(string? Text, decimal? Number, bool? Boolean, Guid? MediaAssetId);

/// <summary>A draft edit for one language, used when several languages are staged in a single save.</summary>
public sealed record ContentTranslationEdit(string Language, ContentValueDto Value);

/// <summary>
/// Owner-only content writes. Every successful write bumps the content revision (invalidating the
/// output and content caches). Returns false when the block key doesn't exist.
/// </summary>
public interface IAdminContentService
{
    /// <summary>Stages a draft edit for one language. The public value is untouched until <see cref="PublishAsync"/>.</summary>
    Task<bool> SetTranslationAsync(string key, string language, ContentValueDto value, string actor, CancellationToken cancellationToken);

    /// <summary>
    /// Stages draft edits for several languages in one load and save, skipping languages whose submitted
    /// value equals the current published value. The public values are untouched until <see cref="PublishAsync"/>.
    /// </summary>
    Task<bool> SetTranslationsAsync(string key, IReadOnlyCollection<ContentTranslationEdit> edits, string actor, CancellationToken cancellationToken);

    /// <summary>Promotes the block's pending drafts to the published values and publishes it (bumps the revision).</summary>
    Task<bool> PublishAsync(string key, string actor, CancellationToken cancellationToken);

    /// <summary>Publishes/unpublishes a block without promoting drafts (used for unpublish).</summary>
    Task<bool> SetPublishedAsync(string key, bool published, string actor, CancellationToken cancellationToken);
}
