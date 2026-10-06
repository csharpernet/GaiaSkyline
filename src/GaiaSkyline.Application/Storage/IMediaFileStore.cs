namespace GaiaSkyline.Application.Storage;

/// <summary>A media file streamed back for serving: its bytes, content type and length.</summary>
public sealed record MediaFileContent(Stream Content, string ContentType, long Length);

/// <summary>
/// Where the responsive rendition files live and are served from (Stage 8 Part B, ADR 0006/0024). The image
/// and hero pipelines generate many stem-named files (<c>{stem}-{w}.{ext}</c>) into a working directory; this
/// seam then makes them durable and serves them.
///
/// Local disk (dev + CI + local-only mode): the working directory IS the served directory
/// (<c>wwwroot/media</c>), so publishing is a no-op and static files serve the bytes — behaviour is
/// byte-identical to before. Azure Blob (deployed production): publishing uploads the stem's files to the
/// <c>media</c> container and a <c>/media</c> endpoint streams them back, with Front Door caching in front.
/// </summary>
public interface IMediaFileStore
{
    /// <summary>Make every <c>{stem}-*</c> file produced in <paramref name="workingDirectory"/> durable.</summary>
    Task PublishStemAsync(string workingDirectory, string stem, CancellationToken cancellationToken);

    /// <summary>Rename all of a stem's files from <paramref name="oldStem"/> to <paramref name="newStem"/>.</summary>
    Task MoveStemAsync(string workingDirectory, string oldStem, string newStem, CancellationToken cancellationToken);

    /// <summary>Open a single media file (<c>{stem}-{w}.{ext}</c>) for serving; null when it does not exist.</summary>
    Task<MediaFileContent?> OpenAsync(string fileName, CancellationToken cancellationToken);

    /// <summary>
    /// True when this store serves media itself (Blob → the app streams <c>/media</c>); false when the host's
    /// static-file middleware serves it (local disk).
    /// </summary>
    bool ServesMedia { get; }
}
