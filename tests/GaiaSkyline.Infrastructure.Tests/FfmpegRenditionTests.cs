using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>
/// Exercises the real FFmpeg transcoder end to end and probes every rendition with ffprobe: the expected
/// dimensions, the expected codec, and no audio stream. Runs where FFmpeg is installed (CI installs it) and
/// skips with a clear reason otherwise. Uses a tiny generated sample clip — no binary is committed. ADR 0017.
/// </summary>
public sealed class FfmpegRenditionTests
{
    private static readonly VideoTranscodingOptions Options = new();

    private static readonly (HeroRenditionKind Kind, string Codec, int Width, int Height)[] Expected =
    [
        (HeroRenditionKind.DesktopAv1, "av1", 1920, 1080),
        (HeroRenditionKind.DesktopVp9, "vp9", 1920, 1080),
        (HeroRenditionKind.DesktopH264, "h264", 1920, 1080),
        (HeroRenditionKind.MobileAv1, "av1", 720, 1280),
        (HeroRenditionKind.MobileVp9, "vp9", 720, 1280),
        (HeroRenditionKind.MobileH264, "h264", 720, 1280),
    ];

    [SkippableFact]
    public async Task Transcoder_produces_every_rendition_at_the_right_size_and_codec_with_no_audio()
    {
        var transcoder = new FfmpegVideoTranscoder(Microsoft.Extensions.Options.Options.Create(Options), NullLogger<FfmpegVideoTranscoder>.Instance);
        Skip.IfNot(transcoder.IsAvailable, "FFmpeg/ffprobe are not installed on this machine.");

        var dir = Path.Combine(Path.GetTempPath(), "gaia-ffmpeg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // A 3 s 1280x720 clip WITH an audio track, so we can prove the audio is stripped.
            var source = Path.Combine(dir, "sample.mp4");
            await RunAsync("ffmpeg",
            [
                "-nostdin", "-y",
                "-f", "lavfi", "-i", "testsrc=duration=3:size=1280x720:rate=30",
                "-f", "lavfi", "-i", "sine=frequency=1000:duration=3",
                "-pix_fmt", "yuv420p", "-shortest", source,
            ]);
            File.Exists(source).Should().BeTrue("the sample clip was generated");

            var set = await transcoder.ProduceHeroRenditionsAsync(
                new HeroTranscodeRequest(source, dir, "probe", TrimStartSec: 0, TrimEndSec: 3, CrossfadeSec: 0, FocalX: 0.5),
                CancellationToken.None);

            set.Renditions.Should().HaveCount(6);
            foreach (var (kind, codec, width, height) in Expected)
            {
                var output = set.Renditions.Single(r => r.Kind == kind);
                var probe = await ProbeAsync(output.FilePath);

                probe.VideoCodec.Should().Be(codec, $"{kind} should be {codec}");
                probe.Width.Should().Be(width, $"{kind} width");
                probe.Height.Should().Be(height, $"{kind} height");
                probe.HasAudio.Should().BeFalse($"{kind} must have no audio stream");
            }

            File.Exists(set.DesktopPosterFramePath).Should().BeTrue();
            File.Exists(set.MobilePosterFramePath).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static async Task<(string? VideoCodec, int Width, int Height, bool HasAudio)> ProbeAsync(string path)
    {
        var json = await RunAsync("ffprobe",
        [
            "-v", "error",
            "-show_entries", "stream=codec_type,codec_name,width,height",
            "-of", "json", path,
        ]);

        using var doc = JsonDocument.Parse(json);
        var streams = doc.RootElement.GetProperty("streams");
        string? videoCodec = null;
        int width = 0, height = 0;
        var hasAudio = false;
        foreach (var s in streams.EnumerateArray())
        {
            var type = s.GetProperty("codec_type").GetString();
            if (type == "video")
            {
                videoCodec = s.GetProperty("codec_name").GetString();
                width = s.GetProperty("width").GetInt32();
                height = s.GetProperty("height").GetInt32();
            }
            else if (type == "audio")
            {
                hasAudio = true;
            }
        }

        return (videoCodec, width, height, hasAudio);
    }

    private static async Task<string> RunAsync(string exe, IReadOnlyList<string> args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var process = Process.Start(psi)!;
        process.StandardInput.Close();
        // Drain both pipes concurrently to avoid a pipe-buffer deadlock (ffmpeg is chatty on stderr).
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{exe} failed ({process.ExitCode}): {stderr}");
        }

        return stdout;
    }
}
