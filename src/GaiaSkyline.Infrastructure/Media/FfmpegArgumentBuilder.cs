using System.Globalization;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// Builds the FFmpeg / ffprobe argument lists for the hero-video renditions and posters. Pure and
/// side-effect-free so the exact flags (audio stripped, CFR, keyframe interval, scale/crop, codec, faststart,
/// crossfade loop) are unit-testable without running FFmpeg (ADR 0017 / Stage 7E-4).
/// </summary>
internal static class FfmpegArgumentBuilder
{
    public const int Fps = 30;

    // Keyframe at least every 2 s at 30 fps.
    public const int KeyframeInterval = Fps * 2;

    // Target bitrates (kbps): desktop ~2.5 Mbps, mobile ~1.0 Mbps — the Stage 7E-4 size budgets.
    private const int DesktopKbps = 2500;
    private const int MobileKbps = 1000;

    public static string OutputFileName(string stem, HeroRenditionKind kind) =>
        $"{stem}-{Slug(kind)}.{Extension(kind)}";

    public static string Extension(HeroRenditionKind kind) =>
        kind is HeroRenditionKind.DesktopH264 or HeroRenditionKind.MobileH264 ? "mp4" : "webm";

    public static (int Width, int Height) Dimensions(HeroRenditionKind kind) =>
        kind.IsMobile() ? (720, 1280) : (1920, 1080);

    /// <summary>ffprobe args: emit duration + the first video stream's dimensions as parseable key=values.</summary>
    public static IReadOnlyList<string> Probe(string sourcePath) =>
    [
        "-v", "error",
        "-select_streams", "v:0",
        "-show_entries", "stream=width,height:format=duration",
        "-of", "default=noprint_wrappers=1",
        sourcePath,
    ];

    /// <summary>Full transcode args for one rendition (input seek + trim, filtergraph, codec, no audio).</summary>
    public static IReadOnlyList<string> Rendition(HeroRenditionKind kind, HeroTranscodeRequest request, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (w, h) = Dimensions(kind);
        var mobile = kind.IsMobile();
        var loop = request.TrimEndSec - request.TrimStartSec;

        var args = new List<string>
        {
            "-y",
            "-ss", Num(request.TrimStartSec),
            "-i", request.SourcePath,
            "-t", Num(loop),
            "-an", // strip audio entirely — the hero video is always muted
        };

        // The video filtergraph: scale/crop to the target frame (focal-point crop for mobile), fix the frame
        // rate, and — when a crossfade is requested — blend the tail into the head for a seamless loop.
        if (request.CrossfadeSec > 0)
        {
            args.Add("-filter_complex");
            args.Add(CrossfadeGraph(ScaleCrop(w, h, mobile, request.FocalX), loop, request.CrossfadeSec));
            args.Add("-map");
            args.Add("[v]");
        }
        else
        {
            args.Add("-vf");
            args.Add($"{ScaleCrop(w, h, mobile, request.FocalX)},fps={Fps}");
        }

        args.Add("-fps_mode");
        args.Add("cfr");
        args.AddRange(CodecArgs(kind, mobile));
        args.Add("-g");
        args.Add(KeyframeInterval.ToString(CultureInfo.InvariantCulture));
        args.Add(outputPath);
        return args;
    }

    /// <summary>Extract a single poster frame (at the loop start) framed exactly like the rendition.</summary>
    public static IReadOnlyList<string> Poster(bool mobile, HeroTranscodeRequest request, string posterPath)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (w, h) = mobile ? (720, 1280) : (1920, 1080);
        return
        [
            "-y",
            "-ss", Num(request.TrimStartSec),
            "-i", request.SourcePath,
            "-frames:v", "1",
            "-vf", ScaleCrop(w, h, mobile, request.FocalX),
            posterPath,
        ];
    }

    // Cover the target box then crop; mobile offsets the horizontal crop by the focal point, desktop centres.
    internal static string ScaleCrop(int width, int height, bool mobile, double focalX)
    {
        var scale = $"scale={width}:{height}:force_original_aspect_ratio=increase";
        var x = mobile
            ? $"(in_w-out_w)*{Num(Math.Clamp(focalX, 0, 1))}"
            : "(in_w-out_w)/2";
        var crop = $"crop={width}:{height}:{x}:(in_h-out_h)/2";
        return $"{scale},{crop}";
    }

    // Seamless-loop crossfade: blend the trailing C seconds back into the opening C seconds. Output is L-C long.
    internal static string CrossfadeGraph(string scaleCrop, double loop, double crossfade)
    {
        var body = Num(loop - crossfade);
        var start = Num(loop - crossfade);
        var end = Num(loop);
        return
            $"[0:v]{scaleCrop},fps={Fps},split[a][b];" +
            $"[a]trim=0:{body},setpts=PTS-STARTPTS[hb];" +
            $"[b]trim={start}:{end},setpts=PTS-STARTPTS[tl];" +
            $"[tl][hb]xfade=transition=fade:duration={Num(crossfade)}:offset=0[v]";
    }

    private static IReadOnlyList<string> CodecArgs(HeroRenditionKind kind, bool mobile)
    {
        var kbps = (mobile ? MobileKbps : DesktopKbps).ToString(CultureInfo.InvariantCulture) + "k";
        return kind switch
        {
            HeroRenditionKind.DesktopH264 or HeroRenditionKind.MobileH264 =>
            [
                "-c:v", "libx264", "-profile:v", "high", "-preset", "medium",
                "-b:v", kbps, "-pix_fmt", "yuv420p", "-movflags", "+faststart",
            ],
            HeroRenditionKind.DesktopVp9 or HeroRenditionKind.MobileVp9 =>
            [
                "-c:v", "libvpx-vp9", "-b:v", kbps, "-row-mt", "1",
                "-deadline", "good", "-cpu-used", "2", "-pix_fmt", "yuv420p",
            ],
            // AV1 via SVT-AV1 (fast, good quality).
            _ =>
            [
                "-c:v", "libsvtav1", "-b:v", kbps, "-preset", "8", "-pix_fmt", "yuv420p",
            ],
        };
    }

    private static string Slug(HeroRenditionKind kind) => kind switch
    {
        HeroRenditionKind.DesktopAv1 => "desktop-av1",
        HeroRenditionKind.DesktopVp9 => "desktop-vp9",
        HeroRenditionKind.DesktopH264 => "desktop-h264",
        HeroRenditionKind.MobileAv1 => "mobile-av1",
        HeroRenditionKind.MobileVp9 => "mobile-vp9",
        HeroRenditionKind.MobileH264 => "mobile-h264",
        _ => kind.ToString().ToLowerInvariant(),
    };

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
