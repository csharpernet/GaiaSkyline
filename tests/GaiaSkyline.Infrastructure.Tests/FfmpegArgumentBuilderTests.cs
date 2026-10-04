using FluentAssertions;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Media;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class FfmpegArgumentBuilderTests
{
    private static HeroTranscodeRequest Request(double crossfade = 0, double focalX = 0.5) =>
        new("/src/in.mp4", "/out", "hero-abc", TrimStartSec: 2, TrimEndSec: 17, CrossfadeSec: crossfade, FocalX: focalX);

    private static string Args(HeroRenditionKind kind, HeroTranscodeRequest req) =>
        string.Join(' ', FfmpegArgumentBuilder.Rendition(kind, req, "/out/x." + FfmpegArgumentBuilder.Extension(kind)));

    [Fact]
    public void Every_rendition_strips_audio_and_fixes_the_frame_rate_and_keyframe_interval()
    {
        foreach (var kind in Enum.GetValues<HeroRenditionKind>())
        {
            var args = Args(kind, Request());
            args.Should().Contain("-an", "the hero video is always muted");
            args.Should().Contain("-fps_mode cfr", "playback must be constant frame rate");
            args.Should().Contain("-g 60", "a keyframe at least every 2 s at 30 fps");
            args.Should().Contain("-ss 2").And.Contain("-t 15", "trim to the 15 s loop window");
        }
    }

    [Theory]
    [InlineData(HeroRenditionKind.DesktopAv1, "libsvtav1")]
    [InlineData(HeroRenditionKind.MobileAv1, "libsvtav1")]
    [InlineData(HeroRenditionKind.DesktopVp9, "libvpx-vp9")]
    [InlineData(HeroRenditionKind.MobileVp9, "libvpx-vp9")]
    [InlineData(HeroRenditionKind.DesktopH264, "libx264")]
    [InlineData(HeroRenditionKind.MobileH264, "libx264")]
    public void Each_rendition_uses_the_expected_codec(HeroRenditionKind kind, string codec)
    {
        Args(kind, Request()).Should().Contain(codec);
    }

    [Fact]
    public void H264_renditions_use_high_profile_and_faststart_in_an_mp4()
    {
        var args = Args(HeroRenditionKind.DesktopH264, Request());
        args.Should().Contain("-profile:v high");
        args.Should().Contain("-movflags +faststart");
        FfmpegArgumentBuilder.Extension(HeroRenditionKind.DesktopH264).Should().Be("mp4");
        FfmpegArgumentBuilder.Extension(HeroRenditionKind.DesktopAv1).Should().Be("webm");
    }

    [Fact]
    public void Desktop_is_sixteen_by_nine_and_mobile_is_nine_by_sixteen()
    {
        FfmpegArgumentBuilder.Dimensions(HeroRenditionKind.DesktopVp9).Should().Be((1920, 1080));
        FfmpegArgumentBuilder.Dimensions(HeroRenditionKind.MobileVp9).Should().Be((720, 1280));
        Args(HeroRenditionKind.DesktopVp9, Request()).Should().Contain("scale=1920:1080");
        Args(HeroRenditionKind.MobileVp9, Request()).Should().Contain("scale=720:1280");
    }

    [Fact]
    public void Mobile_crop_is_offset_by_the_focal_point_while_desktop_is_centred()
    {
        FfmpegArgumentBuilder.ScaleCrop(720, 1280, mobile: true, focalX: 0.25)
            .Should().Contain("(in_w-out_w)*0.25");
        FfmpegArgumentBuilder.ScaleCrop(1920, 1080, mobile: false, focalX: 0.25)
            .Should().Contain("(in_w-out_w)/2", "desktop centres the crop");
    }

    [Fact]
    public void Crossfade_is_only_applied_when_requested_and_produces_a_seamless_loop_graph()
    {
        Args(HeroRenditionKind.DesktopH264, Request(crossfade: 0)).Should().NotContain("xfade");

        var graph = FfmpegArgumentBuilder.CrossfadeGraph("scale=1920:1080", loop: 15, crossfade: 1);
        graph.Should().Contain("split");
        graph.Should().Contain("xfade=transition=fade:duration=1:offset=0");
        graph.Should().Contain("trim=0:14", "the body is the loop minus the crossfade");

        Args(HeroRenditionKind.DesktopH264, Request(crossfade: 1)).Should().Contain("-filter_complex");
    }

    [Fact]
    public void Poster_args_extract_a_single_frame_framed_like_the_rendition()
    {
        var args = string.Join(' ', FfmpegArgumentBuilder.Poster(mobile: true, Request(focalX: 0.8), "/out/poster.png"));
        args.Should().Contain("-frames:v 1");
        args.Should().Contain("scale=720:1280");
        args.Should().Contain("(in_w-out_w)*0.8");
    }

    [Fact]
    public void Output_file_names_encode_the_orientation_and_codec()
    {
        FfmpegArgumentBuilder.OutputFileName("hero-abc", HeroRenditionKind.DesktopAv1).Should().Be("hero-abc-desktop-av1.webm");
        FfmpegArgumentBuilder.OutputFileName("hero-abc", HeroRenditionKind.MobileH264).Should().Be("hero-abc-mobile-h264.mp4");
    }
}
