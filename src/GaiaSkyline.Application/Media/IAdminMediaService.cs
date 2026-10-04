namespace GaiaSkyline.Application.Media;

/// <summary>One entry in a media collection: the asset and whether it's the hero. Order is positional.</summary>
public sealed record CollectionItemDto(Guid MediaAssetId, bool IsHero);

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
}
