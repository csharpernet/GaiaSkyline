using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// Accepts a hero-video upload: validates it against the configured limits, stages the source, creates a new
/// <c>Pending</c> version and enqueues the transcode job. The previous live video is untouched until the new
/// one is ready (ADR 0017 / Stage 7E-4).
/// </summary>
internal sealed class HeroVideoService(
    AppDbContext dbContext,
    IVideoTranscoder transcoder,
    IHeroVideoJobScheduler scheduler,
    IOptions<VideoTranscodingOptions> options,
    TimeProvider clock) : IHeroVideoService
{
    private readonly VideoTranscodingOptions _options = options.Value;

    public async Task<HeroVideoUploadResult> StartUploadAsync(
        HeroVideoUploadRequest request, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ByteSize <= 0)
        {
            return HeroVideoUploadResult.Rejected("The file is empty.");
        }

        if (request.ByteSize > _options.MaxUploadBytes)
        {
            return HeroVideoUploadResult.Rejected(
                $"The file is larger than the {_options.MaxUploadBytes / (1024 * 1024)} MB limit.");
        }

        if (!IsAllowedType(request.ContentType, request.FileName))
        {
            return HeroVideoUploadResult.Rejected("Upload an MP4, MOV or WebM video.");
        }

        if (!transcoder.IsAvailable)
        {
            return HeroVideoUploadResult.Rejected(
                "Video transcoding is not available on this server. Configure FFmpeg (or a provider) first.");
        }

        var id = HeroVideoId.New();
        var extension = Path.GetExtension(request.FileName);
        var sourcePath = HeroVideoPaths.SourcePath(id.Value, string.IsNullOrEmpty(extension) ? ".mp4" : extension);
        Directory.CreateDirectory(HeroVideoPaths.StagingDirectory);
        await using (var file = File.Create(sourcePath))
        {
            await request.Source.CopyToAsync(file, cancellationToken);
        }

        VideoSourceProbe probe;
        try
        {
            probe = await transcoder.ProbeAsync(sourcePath, cancellationToken);
        }
        catch (Exception ex)
        {
            TryDelete(sourcePath);
            return HeroVideoUploadResult.Rejected($"Could not read the video: {ex.Message}");
        }

        if (!probe.HasVideoStream || probe.DurationSec <= 0)
        {
            TryDelete(sourcePath);
            return HeroVideoUploadResult.Rejected("That file has no video stream.");
        }

        if (probe.DurationSec > _options.MaxSourceDurationSec + 0.5)
        {
            TryDelete(sourcePath);
            return HeroVideoUploadResult.Rejected(
                $"The video is longer than the {_options.MaxSourceDurationSec} s limit.");
        }

        var start = Math.Max(0, request.TrimStartSec ?? 0);
        var end = Math.Min(request.TrimEndSec ?? (start + _options.MaxLoopSeconds), probe.DurationSec);
        var loop = end - start;
        if (loop < _options.MinLoopSeconds - 0.01)
        {
            TryDelete(sourcePath);
            return HeroVideoUploadResult.Rejected(
                $"The loop must be at least {_options.MinLoopSeconds:0.#} s — trim a longer window.");
        }

        if (loop > _options.MaxLoopSeconds + 0.01)
        {
            TryDelete(sourcePath);
            return HeroVideoUploadResult.Rejected(
                $"The loop must be at most {_options.MaxLoopSeconds:0.#} s — trim a shorter window.");
        }

        var crossfade = Math.Max(0, request.CrossfadeSec);
        if (crossfade >= loop)
        {
            crossfade = 0;
        }

        var hero = HeroVideo.CreatePending(
            id,
            request.FileName,
            request.ByteSize,
            probe.DurationSec,
            start,
            end,
            crossfade,
            Math.Clamp(request.FocalX, 0, 1),
            clock.GetUtcNow().UtcDateTime,
            actor);

        dbContext.HeroVideos.Add(hero);
        await dbContext.SaveChangesAsync(cancellationToken);

        scheduler.Enqueue(id.Value);
        return HeroVideoUploadResult.Ok(id.Value);
    }

    private bool IsAllowedType(string? contentType, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(contentType)
            && _options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        // Fall back to the extension (browsers don't always send a reliable content type for MOV/WebM).
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".mp4" or ".mov" or ".webm";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
