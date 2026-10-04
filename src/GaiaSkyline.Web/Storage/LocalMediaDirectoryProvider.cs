using GaiaSkyline.Application.Media;

namespace GaiaSkyline.Web.Storage;

/// <summary>
/// Local-disk <see cref="IMediaDirectoryProvider"/>: resolves <c>wwwroot/media</c> for background jobs that
/// have no HTTP context (e.g. the hero-video transcode). Mirrors <see cref="LocalDiskMediaStorage"/> (ADR 0006).
/// </summary>
internal sealed class LocalMediaDirectoryProvider(IWebHostEnvironment environment) : IMediaDirectoryProvider
{
    public string GetMediaDirectory() => Path.Combine(
        string.IsNullOrEmpty(environment.WebRootPath) ? "wwwroot" : environment.WebRootPath, "media");
}
