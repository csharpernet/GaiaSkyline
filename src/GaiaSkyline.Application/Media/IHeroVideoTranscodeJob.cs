namespace GaiaSkyline.Application.Media;

/// <summary>
/// The background job that transcodes a pending hero video: produce the six renditions and two posters, then
/// atomically swap the new version live (retiring the previous one). On failure the version is marked failed
/// and the previous live video is left untouched. Enqueued by <see cref="IHeroVideoJobScheduler"/>.
/// </summary>
public interface IHeroVideoTranscodeJob
{
    Task RunAsync(Guid heroVideoId, CancellationToken cancellationToken);
}

/// <summary>
/// Enqueues the hero-video transcode. Backed by Hangfire when background processing is enabled; a no-op (with a
/// warning) otherwise, matching the dormant-when-disabled background-jobs design.
/// </summary>
public interface IHeroVideoJobScheduler
{
    void Enqueue(Guid heroVideoId);
}
