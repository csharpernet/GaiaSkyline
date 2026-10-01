namespace GaiaSkyline.Application.Storage;

/// <summary>
/// Abstraction over where uploaded media bytes live: local disk under wwwroot/media in dev, Azure
/// Blob Storage in production (see ADR 0006). The concrete implementation and the upload flow that
/// uses it arrive with the admin area in Stage 7; this seam is declared now so the rest of the
/// system depends on the abstraction rather than a storage provider.
/// </summary>
public interface IMediaStorage
{
    /// <summary>Persist media bytes and return the stored location (a <c>BlobUri</c> value).</summary>
    Task<string> SaveAsync(
        Stream content,
        string fileExtension,
        string contentType,
        CancellationToken cancellationToken);

    /// <summary>Remove previously stored media by its <c>BlobUri</c>.</summary>
    Task DeleteAsync(string blobUri, CancellationToken cancellationToken);
}
