using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Application.Media;

/// <summary>What a quick probe of the source clip tells us before we commit to transcoding.</summary>
public sealed record VideoSourceProbe(double DurationSec, int Width, int Height, bool HasVideoStream);

/// <summary>Everything the transcoder needs to produce one hero video's full rendition set.</summary>
public sealed record HeroTranscodeRequest(
    string SourcePath,
    string OutputDirectory,
    string Stem,
    double TrimStartSec,
    double TrimEndSec,
    double CrossfadeSec,
    double FocalX);

/// <summary>One produced rendition file on disk (the job turns the path into a /media URL).</summary>
public sealed record HeroRenditionOutput(
    HeroRenditionKind Kind, string FilePath, int Width, int Height, long ByteSize);

/// <summary>The output of a transcode: the six renditions plus the two poster frames (PNG) to feed the image pipeline.</summary>
public sealed record HeroRenditionSet(
    IReadOnlyList<HeroRenditionOutput> Renditions,
    string DesktopPosterFramePath,
    string MobilePosterFramePath);

/// <summary>
/// Produces the hero background-video renditions from an uploaded source. Kept deliberately high-level so a
/// managed service (e.g. Mux) can satisfy it as readily as a local FFmpeg process (ADR 0017). The local
/// implementation shells out to FFmpeg; where it is not installed/configured, <see cref="IsAvailable"/> is
/// false and the service surfaces a clear error instead of silently failing.
/// </summary>
public interface IVideoTranscoder
{
    /// <summary>True when the backing transcoder (FFmpeg binary, or a provider's credentials) is usable here.</summary>
    bool IsAvailable { get; }

    Task<VideoSourceProbe> ProbeAsync(string sourcePath, CancellationToken cancellationToken);

    /// <summary>
    /// Trim to the loop window, strip audio, scale/crop the desktop (16:9) and mobile (9:16, around the focal
    /// point) renditions in AV1/VP9/H.264, and extract the two poster frames. Writes into the request's output
    /// directory under the request's stem.
    /// </summary>
    Task<HeroRenditionSet> ProduceHeroRenditionsAsync(HeroTranscodeRequest request, CancellationToken cancellationToken);
}
