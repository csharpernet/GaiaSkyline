namespace GaiaSkyline.Application.Media;

/// <summary>
/// Hero-video transcoding configuration, bound from the <c>VideoTranscoding</c> section. Limits are enforced
/// at upload; the FFmpeg paths/provider pick the transcoder (ADR 0017). Secrets (a future provider key) live
/// in User Secrets / the environment, never in source.
/// </summary>
public sealed class VideoTranscodingOptions
{
    public const string SectionName = "VideoTranscoding";

    /// <summary>Max accepted source upload size (default 500 MB).</summary>
    public long MaxUploadBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>Max accepted source duration in seconds (default 60).</summary>
    public int MaxSourceDurationSec { get; set; } = 60;

    /// <summary>Shortest allowed final loop (default 10 s).</summary>
    public double MinLoopSeconds { get; set; } = 10;

    /// <summary>Longest allowed final loop (default 20 s).</summary>
    public double MaxLoopSeconds { get; set; } = 20;

    /// <summary>Accepted source content types (MP4, MOV, WebM).</summary>
    public string[] AllowedContentTypes { get; set; } = ["video/mp4", "video/quicktime", "video/webm"];

    /// <summary>FFmpeg binary (on PATH by default); the dev install is <c>winget install Gyan.FFmpeg</c>.</summary>
    public string FfmpegPath { get; set; } = "ffmpeg";

    /// <summary>ffprobe binary (on PATH by default).</summary>
    public string FfprobePath { get; set; } = "ffprobe";

    /// <summary>Which transcoder to use: <c>Ffmpeg</c> (local) or a future managed provider (<c>Mux</c>).</summary>
    public string Provider { get; set; } = "Ffmpeg";

    /// <summary>Admin warns above this per desktop rendition (default 7 MB).</summary>
    public long DesktopRenditionWarnBytes { get; set; } = 7L * 1024 * 1024;

    /// <summary>Admin warns above this per mobile rendition (default 3 MB).</summary>
    public long MobileRenditionWarnBytes { get; set; } = 3L * 1024 * 1024;
}
