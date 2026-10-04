namespace GaiaSkyline.Domain.Media;

/// <summary>Lifecycle of a hero-video version from upload through transcoding to live (ADR 0017).</summary>
public enum HeroVideoStatus
{
    /// <summary>The source has been uploaded; transcoding has not started.</summary>
    Pending,

    /// <summary>The background transcode job is running.</summary>
    Transcoding,

    /// <summary>Every rendition and poster is ready; the version can be (or is) live.</summary>
    Ready,

    /// <summary>Transcoding failed; the previous live video is untouched.</summary>
    Failed,
}
