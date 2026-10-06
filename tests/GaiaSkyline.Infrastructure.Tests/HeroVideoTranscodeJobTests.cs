using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class HeroVideoTranscodeJobTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    private static HeroVideo ReadyLiveHero(string label)
    {
        var id = HeroVideoId.New();
        var hero = HeroVideo.CreatePending(id, $"{label}.mp4", 1000, 30, 0, 15, 0, 0.5, DateTime.UtcNow, "owner");
        hero.MarkTranscoding();
        hero.SetRenditions(Enum.GetValues<HeroRenditionKind>().Select(k => new HeroVideoRendition(
            HeroVideoRenditionId.New(), id, k, $"/media/{label}-{k}.webm", 1920, 1080, 100)));
        hero.SetPosters($"/media/{label}-d-1600.jpg", null, $"/media/{label}-m-1600.jpg", null);
        hero.MarkReady(DateTime.UtcNow);
        hero.Promote();
        return hero;
    }

    private static HeroVideoTranscodeJob Job(Persistence.AppDbContext context, string mediaDir, bool failing = false) =>
        new(
            context,
            new FakeVideoTranscoder(throwOnProduce: failing),
            new ImageRenditionService(),
            new FixedMediaDirectoryProvider(mediaDir),
            new LocalDiskMediaFileStore(),
            new ContentRevision(),
            TimeProvider.System,
            NullLogger<HeroVideoTranscodeJob>.Instance);

    [Fact]
    public async Task Transcoding_produces_renditions_and_posters_then_swaps_the_new_version_live()
    {
        var mediaDir = Path.Combine(Path.GetTempPath(), "gaia-hero-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mediaDir);
        var previous = ReadyLiveHero("old" + Guid.NewGuid().ToString("N")[..6]);
        var pending = HeroVideo.CreatePending(HeroVideoId.New(), "new.mp4", 5_000_000, 30, 0, 15, 1, 0.3, DateTime.UtcNow, "owner");
        var newId = pending.Id;
        try
        {
            await using (var seed = _fixture.CreateContext())
            {
                seed.HeroVideos.AddRange(previous, pending);
                await seed.SaveChangesAsync();
            }

            Directory.CreateDirectory(HeroVideoPaths.StagingDirectory);
            File.WriteAllBytes(HeroVideoPaths.SourcePath(newId.Value, ".mp4"), [1, 2, 3]);

            await using (var context = _fixture.CreateContext())
            {
                await Job(context, mediaDir).RunAsync(newId.Value, CancellationToken.None);
            }

            await using var verify = _fixture.CreateContext();
            var saved = await verify.HeroVideos.Include(h => h.Renditions).FirstAsync(h => h.Id == newId);
            saved.Status.Should().Be(HeroVideoStatus.Ready, saved.ErrorMessage ?? "(no error recorded)");
            saved.IsLive.Should().BeTrue();
            saved.Renditions.Should().HaveCount(6);
            saved.DesktopPosterBlobUri.Should().NotBeNullOrEmpty();
            saved.MobilePosterBlobUri.Should().NotBeNullOrEmpty();

            var old = await verify.HeroVideos.FirstAsync(h => h.Id == previous.Id);
            old.IsLive.Should().BeFalse("the previous live version is retired in the atomic swap");

            // The posters went through the image pipeline (AVIF/WebP/JPEG at the poster stem).
            File.Exists(Path.Combine(mediaDir, $"{HeroVideoPaths.DesktopPosterStem(newId.Value)}-1600.jpg")).Should().BeTrue();
            File.Exists(Path.Combine(mediaDir, $"{HeroVideoPaths.MobilePosterStem(newId.Value)}-1600.avif")).Should().BeTrue();

            // The staged source is cleaned up on success.
            HeroVideoPaths.FindStagedSource(newId.Value).Should().BeNull();
        }
        finally
        {
            TryDeleteDir(mediaDir);
        }
    }

    [Fact]
    public async Task A_failed_transcode_marks_the_version_failed_and_leaves_the_previous_one_live()
    {
        var mediaDir = Path.Combine(Path.GetTempPath(), "gaia-hero-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(mediaDir);
        var previous = ReadyLiveHero("live" + Guid.NewGuid().ToString("N")[..6]);
        var pending = HeroVideo.CreatePending(HeroVideoId.New(), "bad.mp4", 5_000_000, 30, 0, 15, 0, 0.5, DateTime.UtcNow, "owner");
        var newId = pending.Id;
        try
        {
            await using (var seed = _fixture.CreateContext())
            {
                seed.HeroVideos.AddRange(previous, pending);
                await seed.SaveChangesAsync();
            }

            Directory.CreateDirectory(HeroVideoPaths.StagingDirectory);
            File.WriteAllBytes(HeroVideoPaths.SourcePath(newId.Value, ".mp4"), [1, 2, 3]);

            await using (var context = _fixture.CreateContext())
            {
                await Job(context, mediaDir, failing: true).RunAsync(newId.Value, CancellationToken.None);
            }

            await using var verify = _fixture.CreateContext();
            var failed = await verify.HeroVideos.FirstAsync(h => h.Id == newId);
            failed.Status.Should().Be(HeroVideoStatus.Failed);
            failed.IsLive.Should().BeFalse();
            failed.ErrorMessage.Should().NotBeNullOrEmpty();

            var old = await verify.HeroVideos.FirstAsync(h => h.Id == previous.Id);
            old.IsLive.Should().BeTrue("a failed transcode must not disturb the live video");
        }
        finally
        {
            TryDeleteDir(mediaDir);
        }
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }
}
