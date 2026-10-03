using GaiaSkyline.Application.Storage;

namespace GaiaSkyline.Web.Storage;

/// <summary>
/// Local-disk <see cref="IMediaStorage"/> for development: writes under <c>wwwroot/media</c> and
/// returns a relative <c>/media/...</c> URL. Azure Blob replaces this in Stage 8 (ADR 0006).
/// </summary>
internal sealed class LocalDiskMediaStorage(IWebHostEnvironment environment) : IMediaStorage
{
    private string MediaRoot => Path.Combine(
        string.IsNullOrEmpty(environment.WebRootPath) ? "wwwroot" : environment.WebRootPath, "media");

    public async Task<string> SaveAsync(Stream content, string fileExtension, string contentType, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        Directory.CreateDirectory(MediaRoot);

        var ext = fileExtension.StartsWith('.') ? fileExtension : "." + fileExtension;
        var name = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(MediaRoot, name);

        await using (var file = File.Create(path))
        {
            await content.CopyToAsync(file, cancellationToken);
        }

        return $"/media/{name}";
    }

    public Task DeleteAsync(string blobUri, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(blobUri) && blobUri.StartsWith("/media/", StringComparison.Ordinal))
        {
            var path = Path.Combine(MediaRoot, Path.GetFileName(blobUri));
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        return Task.CompletedTask;
    }
}
