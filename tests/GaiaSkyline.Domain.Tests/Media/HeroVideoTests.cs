using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Domain.Tests.Media;

public class HeroVideoTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static HeroVideo Pending(double start = 0, double end = 15, double crossfade = 1, double focalX = 0.5) =>
        HeroVideo.CreatePending(
            HeroVideoId.New(), "clip.mp4", 10_000_000, 30, start, end, crossfade, focalX, Now, "owner");

    private static IEnumerable<HeroVideoRendition> SixRenditions(HeroVideoId id) =>
        Enum.GetValues<HeroRenditionKind>().Select(k => new HeroVideoRendition(
            HeroVideoRenditionId.New(), id, k, $"/media/hero-{k}.{(k.ToString().Contains("H264") ? "mp4" : "webm")}",
            k.IsMobile() ? 720 : 1920, k.IsMobile() ? 1280 : 1080, 1000));

    [Fact]
    public void Create_pending_sets_the_settings_and_status()
    {
        var hero = Pending(start: 2, end: 14, crossfade: 1.5, focalX: 0.25);

        hero.Status.Should().Be(HeroVideoStatus.Pending);
        hero.IsLive.Should().BeFalse();
        hero.LoopDurationSec.Should().BeApproximately(12, 0.001);
        hero.FocalX.Should().Be(0.25);
    }

    [Theory]
    [InlineData(5, 5)]   // end not after start
    [InlineData(10, 5)]  // end before start
    public void Create_rejects_a_non_positive_loop(double start, double end)
    {
        var act = () => Pending(start, end);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_rejects_a_crossfade_not_shorter_than_the_loop()
    {
        var act = () => Pending(start: 0, end: 10, crossfade: 10);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Create_rejects_a_focal_point_outside_0_to_1(double focalX)
    {
        var act = () => Pending(focalX: focalX);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Mark_ready_requires_all_six_renditions_and_both_posters()
    {
        var hero = Pending();
        hero.MarkTranscoding();

        // No renditions yet.
        FluentActions.Invoking(() => hero.MarkReady(Now)).Should().Throw<InvalidOperationException>();

        hero.SetRenditions(SixRenditions(hero.Id));
        // Renditions but no posters.
        FluentActions.Invoking(() => hero.MarkReady(Now)).Should().Throw<InvalidOperationException>();

        hero.SetPosters("/media/d-1600.jpg", "lqip", "/media/m-1600.jpg", "lqip");
        hero.MarkReady(Now);
        hero.Status.Should().Be(HeroVideoStatus.Ready);
    }

    [Fact]
    public void Only_a_ready_video_can_go_live()
    {
        var hero = Pending();
        FluentActions.Invoking(hero.Promote).Should().Throw<InvalidOperationException>();

        hero.MarkTranscoding();
        hero.SetRenditions(SixRenditions(hero.Id));
        hero.SetPosters("/media/d-1600.jpg", null, "/media/m-1600.jpg", null);
        hero.MarkReady(Now);
        hero.Promote();

        hero.IsLive.Should().BeTrue();
        hero.Retire();
        hero.IsLive.Should().BeFalse();
    }

    [Fact]
    public void Mark_failed_records_the_error_and_leaves_it_not_live()
    {
        var hero = Pending();
        hero.MarkTranscoding();
        hero.MarkFailed("boom");

        hero.Status.Should().Be(HeroVideoStatus.Failed);
        hero.ErrorMessage.Should().Be("boom");
        hero.IsLive.Should().BeFalse();
    }
}
