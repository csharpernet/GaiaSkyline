namespace GaiaSkyline.Application.Media;

/// <summary>One entry in a media collection: the asset and whether it's the hero. Order is positional.</summary>
public sealed record CollectionItemDto(Guid MediaAssetId, bool IsHero);

/// <summary>Outcome of a soft-delete attempt.</summary>
public enum MediaDeleteResult
{
    Deleted,
    InUse,
    NotFound,
}

/// <summary>
/// Owner-only media writes. Uploads generate the responsive raster set; collection updates replace the
/// whole ordered item list (so one call covers reorder, add, remove and set-hero). Each bumps the
/// content revision.
/// </summary>
public interface IAdminMediaService
{
    /// <summary>Stores the responsive renditions in <paramref name="destinationDirectory"/> and returns the new asset id.</summary>
    Task<Guid> UploadImageAsync(Stream content, string destinationDirectory, string? altText, string actor, CancellationToken cancellationToken);

    Task<bool> SetCollectionItemsAsync(string collectionKey, IReadOnlyList<CollectionItemDto> items, string actor, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the per-language alt text for an asset: each entry upserts its language; a blank value clears
    /// that language. Bumps the content revision (alt text is rendered on the public site). False if unknown.
    /// </summary>
    Task<bool> SetAltTextsAsync(Guid assetId, IReadOnlyDictionary<string, string?> altByLanguage, string actor, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces an image's binary while keeping the asset id and URL: the renditions are regenerated at the
    /// same stem, so every content/collection reference survives. False if the asset is unknown or not an
    /// image. Bumps the content revision.
    /// </summary>
    Task<bool> ReplaceImageAsync(Guid assetId, Stream content, string destinationDirectory, string actor, CancellationToken cancellationToken);

    /// <summary>Soft-deletes an asset (hidden from the library and pickers) unless it is still in use.</summary>
    Task<MediaDeleteResult> SoftDeleteAsync(Guid assetId, string actor, CancellationToken cancellationToken);

    /// <summary>Restores a soft-deleted asset. False if unknown.</summary>
    Task<bool> RestoreAsync(Guid assetId, string actor, CancellationToken cancellationToken);
}
