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

/// <summary>Outcome of an SEO-filename rename attempt.</summary>
public enum MediaRenameResult
{
    Renamed,

    /// <summary>The requested name slugified to the current filename — nothing to do.</summary>
    Unchanged,

    /// <summary>The requested name has no usable slug characters (e.g. only punctuation).</summary>
    InvalidName,

    /// <summary>No such asset.</summary>
    NotFound,

    /// <summary>The asset is not a pipeline raster with a renamable stem (e.g. a video, or legacy URL).</summary>
    Unsupported,
}

/// <summary>
/// Owner-only media writes. Uploads generate the responsive raster set; collection updates replace the
/// whole ordered item list (so one call covers reorder, add, remove and set-hero). Each bumps the
/// content revision.
/// </summary>
public interface IAdminMediaService
{
    /// <summary>
    /// Stores the responsive renditions in <paramref name="destinationDirectory"/> and returns the new asset id.
    /// The on-disk filename (SEO stem) is a slug of <paramref name="title"/> (usually the uploaded file name),
    /// made unique; it falls back to the GUID when the title has no usable slug. The GUID id stays internal.
    /// </summary>
    Task<Guid> UploadImageAsync(Stream content, string destinationDirectory, string? altText, string? title, string actor, CancellationToken cancellationToken);

    /// <summary>
    /// Renames an image's SEO filename: moves the renditions to the new stem and updates the URL, keeping the id
    /// (so every content/collection reference survives). The old stem is remembered so its previous URL 301s to
    /// the new one. Bumps the content revision on success.
    /// </summary>
    Task<MediaRenameResult> RenameImageAsync(Guid assetId, string newTitle, string destinationDirectory, string actor, CancellationToken cancellationToken);

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
