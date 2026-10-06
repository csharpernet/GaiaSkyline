using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using GaiaSkyline.Application.Storage;

namespace GaiaSkyline.Web.Storage;

/// <summary>
/// Azure Blob <see cref="IMediaStorage"/> for deployed production (ADR 0006/0024): single-file saves (e.g.
/// generated guest PDFs) into a container, returning a <c>/media/{name}</c> URL. The local-disk equivalent
/// is <see cref="LocalDiskMediaStorage"/>. Not exercised in local-only mode.
/// </summary>
internal sealed class BlobMediaStorage(BlobContainerClient container) : IMediaStorage
{
    public async Task<string> SaveAsync(
        Stream content, string fileExtension, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        var ext = fileExtension.StartsWith('.') ? fileExtension : "." + fileExtension;
        var name = $"{Guid.NewGuid():N}{ext}";

        await container.GetBlobClient(name).UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);

        return $"/media/{name}";
    }

    public async Task DeleteAsync(string blobUri, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(blobUri) && blobUri.StartsWith("/media/", StringComparison.Ordinal))
        {
            await container.GetBlobClient(Path.GetFileName(blobUri)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        }
    }
}
