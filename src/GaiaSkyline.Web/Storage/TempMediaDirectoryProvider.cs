using GaiaSkyline.Application.Media;

namespace GaiaSkyline.Web.Storage;

/// <summary>
/// In a deployed environment the rendition pipeline generates into a writable temp directory, which is then
/// published to Blob by <see cref="BlobMediaFileStore"/> (Stage 8 Part B, ADR 0024) — the App Service file
/// system is not the durable media home. Local/dev uses <see cref="LocalMediaDirectoryProvider"/> instead.
/// </summary>
internal sealed class TempMediaDirectoryProvider : IMediaDirectoryProvider
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gaiaskyline-media-work");

    public string GetMediaDirectory()
    {
        Directory.CreateDirectory(_directory);
        return _directory;
    }
}
