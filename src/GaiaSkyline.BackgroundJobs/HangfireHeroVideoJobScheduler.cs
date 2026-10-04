using GaiaSkyline.Application.Media;
using Hangfire;

namespace GaiaSkyline.BackgroundJobs;

/// <summary>
/// Enqueues hero-video transcoding as a Hangfire job, so the upload request returns immediately and the
/// (minutes-long) transcode runs in the background with automatic retries (ADR 0017 / Stage 7E-4).
/// </summary>
internal sealed class HangfireHeroVideoJobScheduler(IBackgroundJobClient backgroundJobs) : IHeroVideoJobScheduler
{
    public void Enqueue(Guid heroVideoId) =>
        backgroundJobs.Enqueue<IHeroVideoTranscodeJob>(job => job.RunAsync(heroVideoId, CancellationToken.None));
}
