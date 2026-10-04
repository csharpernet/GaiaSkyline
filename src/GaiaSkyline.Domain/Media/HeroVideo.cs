using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Media;

/// <summary>
/// One owner-managed hero background-video version: the source settings (trim window, loop crossfade,
/// mobile focal point), the six transcoded renditions, the two posters, and the transcode lifecycle.
/// Kept separate from the general media library because of its shape and its atomic-swap behaviour: a new
/// version transcodes while the previous one stays <see cref="IsLive"/>, and the swap happens in a single
/// transaction once the new one is <see cref="HeroVideoStatus.Ready"/> (ADR 0017 / Stage 7E-4).
/// </summary>
public sealed class HeroVideo : Entity<HeroVideoId>
{
    private readonly List<HeroVideoRendition> _renditions = [];

    // Required by EF Core's materialization.
    private HeroVideo()
    {
    }

    private HeroVideo(
        HeroVideoId id,
        string sourceFileName,
        long sourceByteSize,
        double sourceDurationSec,
        double trimStartSec,
        double trimEndSec,
        double crossfadeSec,
        double focalX,
        DateTime createdAtUtc,
        string createdBy)
    {
        Id = id;
        SourceFileName = sourceFileName;
        SourceByteSize = sourceByteSize;
        SourceDurationSec = sourceDurationSec;
        TrimStartSec = trimStartSec;
        TrimEndSec = trimEndSec;
        CrossfadeSec = crossfadeSec;
        FocalX = focalX;
        Status = HeroVideoStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy;
    }

    /// <summary>Create a new version in <see cref="HeroVideoStatus.Pending"/>, ready to be transcoded.</summary>
    public static HeroVideo CreatePending(
        HeroVideoId id,
        string sourceFileName,
        long sourceByteSize,
        double sourceDurationSec,
        double trimStartSec,
        double trimEndSec,
        double crossfadeSec,
        double focalX,
        DateTime createdAtUtc,
        string createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);
        ArgumentOutOfRangeException.ThrowIfNegative(sourceByteSize);
        ValidateSettings(trimStartSec, trimEndSec, crossfadeSec, focalX);

        return new HeroVideo(
            id, sourceFileName.Trim(), sourceByteSize, sourceDurationSec,
            trimStartSec, trimEndSec, crossfadeSec, focalX, createdAtUtc, createdBy.Trim());
    }

    public string SourceFileName { get; private set; } = null!;

    public long SourceByteSize { get; private set; }

    public double SourceDurationSec { get; private set; }

    /// <summary>Loop start within the source, in seconds.</summary>
    public double TrimStartSec { get; private set; }

    /// <summary>Loop end within the source, in seconds.</summary>
    public double TrimEndSec { get; private set; }

    /// <summary>Length of the final loop, in seconds.</summary>
    public double LoopDurationSec => TrimEndSec - TrimStartSec;

    /// <summary>Crossfade applied at the loop point for seamlessness; 0 disables it.</summary>
    public double CrossfadeSec { get; private set; }

    /// <summary>Horizontal focal point (0 = left, 1 = right) the mobile portrait crop centres on.</summary>
    public double FocalX { get; private set; }

    public HeroVideoStatus Status { get; private set; }

    /// <summary>True for the single version the public site plays. A new version only becomes live once ready.</summary>
    public bool IsLive { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? DesktopPosterBlobUri { get; private set; }

    public string? DesktopPosterLqip { get; private set; }

    public string? MobilePosterBlobUri { get; private set; }

    public string? MobilePosterLqip { get; private set; }

    public IReadOnlyCollection<HeroVideoRendition> Renditions => _renditions.AsReadOnly();

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ReadyAtUtc { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public void MarkTranscoding()
    {
        if (Status is not (HeroVideoStatus.Pending or HeroVideoStatus.Failed))
        {
            throw new InvalidOperationException($"Cannot start transcoding from status {Status}.");
        }

        Status = HeroVideoStatus.Transcoding;
        ErrorMessage = null;
    }

    /// <summary>Replace the rendition set (the transcode job produces all six at once).</summary>
    public void SetRenditions(IEnumerable<HeroVideoRendition> renditions)
    {
        ArgumentNullException.ThrowIfNull(renditions);
        _renditions.Clear();
        _renditions.AddRange(renditions);
    }

    public void SetPosters(string desktopBlobUri, string? desktopLqip, string mobileBlobUri, string? mobileLqip)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(desktopBlobUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileBlobUri);

        DesktopPosterBlobUri = desktopBlobUri.Trim();
        DesktopPosterLqip = string.IsNullOrWhiteSpace(desktopLqip) ? null : desktopLqip.Trim();
        MobilePosterBlobUri = mobileBlobUri.Trim();
        MobilePosterLqip = string.IsNullOrWhiteSpace(mobileLqip) ? null : mobileLqip.Trim();
    }

    /// <summary>All six renditions and both posters are present and the version is ready to go live.</summary>
    public void MarkReady(DateTime readyAtUtc)
    {
        if (_renditions.Count != 6)
        {
            throw new InvalidOperationException("A hero video needs all six renditions before it is ready.");
        }

        if (string.IsNullOrWhiteSpace(DesktopPosterBlobUri) || string.IsNullOrWhiteSpace(MobilePosterBlobUri))
        {
            throw new InvalidOperationException("A hero video needs both posters before it is ready.");
        }

        Status = HeroVideoStatus.Ready;
        ErrorMessage = null;
        ReadyAtUtc = readyAtUtc;
    }

    public void MarkFailed(string error)
    {
        Status = HeroVideoStatus.Failed;
        ErrorMessage = string.IsNullOrWhiteSpace(error) ? "Transcoding failed." : error.Trim();
    }

    /// <summary>Make this the live version. Only a ready version can go live.</summary>
    public void Promote()
    {
        if (Status != HeroVideoStatus.Ready)
        {
            throw new InvalidOperationException($"Only a ready hero video can go live (status is {Status}).");
        }

        IsLive = true;
    }

    /// <summary>Step the previous version down when a new one goes live.</summary>
    public void Retire() => IsLive = false;

    private static void ValidateSettings(double trimStartSec, double trimEndSec, double crossfadeSec, double focalX)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(trimStartSec);
        if (trimEndSec <= trimStartSec)
        {
            throw new ArgumentOutOfRangeException(nameof(trimEndSec), "The loop end must be after the loop start.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(crossfadeSec);
        if (crossfadeSec >= trimEndSec - trimStartSec)
        {
            throw new ArgumentOutOfRangeException(nameof(crossfadeSec), "The crossfade must be shorter than the loop.");
        }

        if (focalX is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(focalX), "The focal point must be between 0 and 1.");
        }
    }
}
