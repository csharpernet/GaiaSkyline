using System.Text;
using FluentAssertions;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class HeroVideoServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    private static readonly VideoTranscodingOptions Options = new();

    private static HeroVideoService Service(
        Persistence.AppDbContext context, IVideoTranscoder transcoder, IHeroVideoJobScheduler scheduler) =>
        new(context, transcoder, scheduler, Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System);

    private static MemoryStream Tiny() => new(Encoding.ASCII.GetBytes("not-a-real-video"));

    [Fact]
    public async Task Rejects_a_file_over_the_size_limit()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context, new FakeVideoTranscoder(), new RecordingHeroVideoJobScheduler());

        var request = new HeroVideoUploadRequest(Tiny(), "big.mp4", Options.MaxUploadBytes + 1, "video/mp4", null, null, 0, 0.5);
        var result = await service.StartUploadAsync(request, "owner", CancellationToken.None);

        result.Accepted.Should().BeFalse();
        result.Error.Should().Contain("larger");
    }

    [Fact]
    public async Task Rejects_a_non_video_type()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context, new FakeVideoTranscoder(), new RecordingHeroVideoJobScheduler());

        var request = new HeroVideoUploadRequest(Tiny(), "photo.png", 1000, "image/png", null, null, 0, 0.5);
        var result = await service.StartUploadAsync(request, "owner", CancellationToken.None);

        result.Accepted.Should().BeFalse();
    }

    [Fact]
    public async Task Rejects_when_the_transcoder_is_unavailable()
    {
        await using var context = _fixture.CreateContext();
        var service = Service(context, new FakeVideoTranscoder(available: false), new RecordingHeroVideoJobScheduler());

        var request = new HeroVideoUploadRequest(Tiny(), "clip.mp4", 1000, "video/mp4", null, null, 0, 0.5);
        var result = await service.StartUploadAsync(request, "owner", CancellationToken.None);

        result.Accepted.Should().BeFalse();
        result.Error.Should().Contain("not available");
    }

    [Fact]
    public async Task Rejects_a_loop_shorter_than_the_minimum()
    {
        await using var context = _fixture.CreateContext();
        // Source is only 5 s — shorter than the 10 s minimum loop.
        var transcoder = new FakeVideoTranscoder(probe: new VideoSourceProbe(5, 1920, 1080, true));
        var service = Service(context, transcoder, new RecordingHeroVideoJobScheduler());

        var request = new HeroVideoUploadRequest(Tiny(), "short.mp4", 1000, "video/mp4", null, null, 0, 0.5);
        var result = await service.StartUploadAsync(request, "owner", CancellationToken.None);

        result.Accepted.Should().BeFalse();
        result.Error.Should().Contain("at least");
    }

    [Fact]
    public async Task Accepts_a_valid_upload_creates_a_pending_version_and_enqueues_it()
    {
        await using var context = _fixture.CreateContext();
        var scheduler = new RecordingHeroVideoJobScheduler();
        var service = Service(context, new FakeVideoTranscoder(), scheduler);

        var request = new HeroVideoUploadRequest(Tiny(), "hero.mp4", 5_000_000, "video/mp4", 3, 18, 1, 0.3);
        var result = await service.StartUploadAsync(request, "owner", CancellationToken.None);

        result.Accepted.Should().BeTrue();
        result.HeroVideoId.Should().NotBeNull();
        scheduler.Enqueued.Should().ContainSingle().Which.Should().Be(result.HeroVideoId!.Value);

        await using var verify = _fixture.CreateContext();
        var id = HeroVideoId.From(result.HeroVideoId!.Value);
        var hero = await verify.HeroVideos.FirstAsync(h => h.Id == id);
        hero.Status.Should().Be(Domain.Media.HeroVideoStatus.Pending);
        hero.LoopDurationSec.Should().BeApproximately(15, 0.01);
        hero.FocalX.Should().Be(0.3);

        // Clean up the staged source this test wrote to the temp dir.
        var staged = HeroVideoPaths.FindStagedSource(result.HeroVideoId!.Value);
        if (staged is not null)
        {
            File.Delete(staged);
        }
    }
}
