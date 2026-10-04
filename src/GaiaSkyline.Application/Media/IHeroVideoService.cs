namespace GaiaSkyline.Application.Media;

/// <summary>An owner's hero-video upload with the loop/crop settings chosen in the admin.</summary>
public sealed record HeroVideoUploadRequest(
    Stream Source,
    string FileName,
    long ByteSize,
    string? ContentType,
    double? TrimStartSec,
    double? TrimEndSec,
    double CrossfadeSec,
    double FocalX);

/// <summary>Outcome of accepting (or rejecting) a hero-video upload.</summary>
public sealed record HeroVideoUploadResult(bool Accepted, Guid? HeroVideoId, string? Error)
{
    public static HeroVideoUploadResult Ok(Guid id) => new(true, id, null);

    public static HeroVideoUploadResult Rejected(string error) => new(false, null, error);
}

/// <summary>
/// Owner writes for the hero background video: accept an uploaded source, validate it against the configured
/// limits, persist a new <c>Pending</c> version and enqueue the transcode job. The previous video stays live
/// until the new one is fully ready (ADR 0017 / Stage 7E-4).
/// </summary>
public interface IHeroVideoService
{
    Task<HeroVideoUploadResult> StartUploadAsync(HeroVideoUploadRequest request, string actor, CancellationToken cancellationToken);
}
