namespace GaiaSkyline.Application.Media;

/// <summary>
/// The on-disk directory where media renditions are written and served from (<c>wwwroot/media</c> in dev).
/// Abstracted so background jobs (which have no HTTP context) can resolve it without depending on the web host.
/// Mirrors the local-disk storage seam in ADR 0006.
/// </summary>
public interface IMediaDirectoryProvider
{
    string GetMediaDirectory();
}
