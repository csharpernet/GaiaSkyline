using GaiaSkyline.Application.Media;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// Fallback <see cref="IHeroVideoJobScheduler"/> when background processing is disabled: it records a warning
/// (no Hangfire server exists to run the transcode). The Hangfire-backed scheduler overrides this when enabled,
/// matching the dormant-when-disabled background-jobs design.
/// </summary>
internal sealed class LoggingHeroVideoJobScheduler(ILogger<LoggingHeroVideoJobScheduler> logger) : IHeroVideoJobScheduler
{
    public void Enqueue(Guid heroVideoId) =>
        logger.LogWarning(
            "Background jobs are disabled; hero video {Id} will not transcode until a job runner is enabled.",
            heroVideoId);
}
