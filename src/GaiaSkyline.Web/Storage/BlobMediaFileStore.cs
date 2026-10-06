using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GaiaSkyline.Application.Storage;

namespace GaiaSkyline.Web.Storage;

/// <summary>
/// Azure Blob <see cref="IMediaFileStore"/> for deployed production (ADR 0024). Rendition files are generated
/// into a local temp working directory, then published to the <c>media</c> container; the <c>/media</c>
/// endpoint streams them back and Front Door caches them. Files are immutable (a replace keeps the filename
/// but bumps the asset <c>?v=</c> token), so they carry a long-lived immutable cache header.
///
/// Reached through the App Service managed identity (no storage keys). Not exercised in local-only mode —
/// deploy verification is a DEPLOYMENT PHASE task.
/// </summary>
internal sealed class BlobMediaFileStore(BlobContainerClient container) : IMediaFileStore
{
    private const string ImmutableCacheControl = "public, max-age=31536000, immutable";

    public bool ServesMedia => true;

    public async Task PublishStemAsync(string workingDirectory, string stem, CancellationToken cancellationToken)
    {
        foreach (var path in Directory.EnumerateFiles(workingDirectory, $"{stem}-*"))
        {
            await UploadAsync(Path.GetFileName(path), path, cancellationToken);
        }
    }

    public async Task MoveStemAsync(
        string workingDirectory, string oldStem, string newStem, CancellationToken cancellationToken)
    {
        // Rename is infrequent and the files are small; a streamed copy within the container avoids needing a
        // readable source URI (public access is disabled) or a user-delegation SAS.
        await foreach (var item in container.GetBlobsAsync(prefix: $"{oldStem}-", cancellationToken: cancellationToken))
        {
            var source = container.GetBlobClient(item.Name);
            var targetName = string.Concat(newStem, item.Name.AsSpan(oldStem.Length));
            var target = container.GetBlobClient(targetName);

            var download = await source.DownloadStreamingAsync(cancellationToken: cancellationToken);
            await target.UploadAsync(
                download.Value.Content,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = ContentTypeFor(targetName),
                        CacheControl = ImmutableCacheControl,
                    },
                },
                cancellationToken);
            await source.DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }

    public async Task<MediaFileContent?> OpenAsync(string fileName, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(fileName);
        if (!await blob.ExistsAsync(cancellationToken))
        {
            return null;
        }

        var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return new MediaFileContent(
            download.Value.Content,
            download.Value.Details.ContentType ?? ContentTypeFor(fileName),
            download.Value.Details.ContentLength);
    }

    /// <summary>The one-off migration: upload every file in a local media directory to the container.</summary>
    public async Task PublishDirectoryAsync(string localDirectory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(localDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(localDirectory))
        {
            await UploadAsync(Path.GetFileName(path), path, cancellationToken);
        }
    }

    private async Task UploadAsync(string name, string localPath, CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(localPath);
        await container.GetBlobClient(name).UploadAsync(
            file,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = ContentTypeFor(name),
                    CacheControl = ImmutableCacheControl,
                },
            },
            cancellationToken);
    }

    private static string ContentTypeFor(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            ".png" => "image/png",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            _ => "application/octet-stream",
        };
}
