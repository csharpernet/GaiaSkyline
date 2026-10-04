using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// Transcodes a pending hero video, pushes both poster frames through the image pipeline, then atomically swaps
/// the new version live (retiring the previous one) in a single transaction. On failure the version is marked
/// failed and the previous live video is left playing (ADR 0017 / Stage 7E-4).
/// </summary>
internal sealed class HeroVideoTranscodeJob(
    AppDbContext dbContext,
    IVideoTranscoder transcoder,
    IImageRenditionService images,
    IMediaDirectoryProvider mediaDirectory,
    IContentRevision revision,
    TimeProvider clock,
    ILogger<HeroVideoTranscodeJob> logger) : IHeroVideoTranscodeJob
{
    public async Task RunAsync(Guid heroVideoId, CancellationToken cancellationToken)
    {
        var id = HeroVideoId.From(heroVideoId);
        var hero = await dbContext.HeroVideos
            .Include(h => h.Renditions)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        if (hero is null)
        {
            logger.LogWarning("Hero video {Id} not found; nothing to transcode.", heroVideoId);
            return;
        }

        var source = HeroVideoPaths.FindStagedSource(heroVideoId);
        if (source is null)
        {
            hero.MarkFailed("The uploaded source file is no longer available.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        hero.MarkTranscoding();
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var mediaDir = mediaDirectory.GetMediaDirectory();
            var stem = HeroVideoPaths.Stem(heroVideoId);
            var set = await transcoder.ProduceHeroRenditionsAsync(
                new HeroTranscodeRequest(source, mediaDir, stem, hero.TrimStartSec, hero.TrimEndSec, hero.CrossfadeSec, hero.FocalX),
                cancellationToken);

            var renditions = set.Renditions.Select(o => new HeroVideoRendition(
                HeroVideoRenditionId.New(), id, o.Kind, $"/media/{Path.GetFileName(o.FilePath)}", o.Width, o.Height, o.ByteSize));
            hero.SetRenditions(renditions);

            var desktopPoster = await GeneratePosterAsync(set.DesktopPosterFramePath, mediaDir, HeroVideoPaths.DesktopPosterStem(heroVideoId), cancellationToken);
            var mobilePoster = await GeneratePosterAsync(set.MobilePosterFramePath, mediaDir, HeroVideoPaths.MobilePosterStem(heroVideoId), cancellationToken);
            hero.SetPosters(desktopPoster.BlobUri, desktopPoster.Lqip, mobilePoster.BlobUri, mobilePoster.Lqip);

            // Atomic swap: retire every other live version and promote this one in a single SaveChanges (one
            // transaction), so the public hero flips over all at once and the previous one plays until now.
            hero.MarkReady(clock.GetUtcNow().UtcDateTime);
            var liveOthers = await dbContext.HeroVideos
                .Where(h => h.IsLive && h.Id != id)
                .ToListAsync(cancellationToken);
            foreach (var other in liveOthers)
            {
                other.Retire();
            }

            hero.Promote();
            await dbContext.SaveChangesAsync(cancellationToken);

            revision.Bump(); // the public hero changed → invalidate caches
            CleanUp(source, set);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Hero video {Id} is live.", heroVideoId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Hero video {Id} transcode failed.", heroVideoId);
            hero.MarkFailed(ex.Message);
            await dbContext.SaveChangesAsync(cancellationToken);
            TryDelete(source);
        }
    }

    private async Task<(string BlobUri, string? Lqip)> GeneratePosterAsync(
        string framePath, string mediaDir, string stem, CancellationToken cancellationToken)
    {
        await using var frame = File.OpenRead(framePath);
        var result = await images.GenerateAsync(frame, mediaDir, stem, cancellationToken);
        return ($"/media/{stem}-1600.jpg", result.Lqip);
    }

    private static void CleanUp(string source, HeroRenditionSet set)
    {
        TryDelete(source);
        TryDelete(set.DesktopPosterFramePath);
        TryDelete(set.MobilePosterFramePath);
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
