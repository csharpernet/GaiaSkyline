using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Application.Media;

/// <summary>One rendition for the public markup / admin size list.</summary>
public sealed record HeroRenditionDto(
    HeroRenditionKind Kind, string BlobUri, string ContentType, int Width, int Height, long ByteSize);

/// <summary>The live hero video for the public site: its renditions and posters.</summary>
public sealed record HeroVideoDto(
    Guid Id,
    IReadOnlyList<HeroRenditionDto> Renditions,
    string DesktopPosterBlobUri,
    string? DesktopPosterLqip,
    string MobilePosterBlobUri,
    string? MobilePosterLqip,
    double LoopDurationSec);

/// <summary>The current hero video for the admin: status, settings and rendition sizes (with warnings in 7E-4b).</summary>
public sealed record HeroVideoAdminDto(
    Guid Id,
    HeroVideoStatus Status,
    bool IsLive,
    string? ErrorMessage,
    string SourceFileName,
    double TrimStartSec,
    double TrimEndSec,
    double CrossfadeSec,
    double FocalX,
    IReadOnlyList<HeroRenditionDto> Renditions,
    string? DesktopPosterBlobUri,
    string? MobilePosterBlobUri,
    DateTime CreatedAtUtc,
    DateTime? ReadyAtUtc,
    string CreatedBy);

/// <summary>
/// Reads for the hero video: the live version the public site plays, and the current (latest) version with its
/// transcode status for the admin.
/// </summary>
public interface IHeroVideoReadService
{
    /// <summary>The live hero video, or null when none has been published yet (fall back to the seeded placeholder).</summary>
    Task<HeroVideoDto?> GetLiveAsync(CancellationToken cancellationToken);

    /// <summary>The most recently created version (any status), for the admin panel; null if none exist.</summary>
    Task<HeroVideoAdminDto?> GetCurrentAsync(CancellationToken cancellationToken);
}
