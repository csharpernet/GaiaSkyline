using System.Diagnostics;
using System.Globalization;
using System.Text;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// <see cref="IVideoTranscoder"/> backed by a local FFmpeg install (ADR 0017). Shells out to ffmpeg/ffprobe
/// using <see cref="FfmpegArgumentBuilder"/>. Where the binaries are not found it reports
/// <see cref="IsAvailable"/> = false so the service fails with a clear message rather than hanging.
/// </summary>
internal sealed class FfmpegVideoTranscoder(
    IOptions<VideoTranscodingOptions> options,
    ILogger<FfmpegVideoTranscoder> logger) : IVideoTranscoder
{
    private readonly VideoTranscodingOptions _options = options.Value;

    public bool IsAvailable =>
        ResolveExecutable(_options.FfmpegPath) is not null && ResolveExecutable(_options.FfprobePath) is not null;

    public async Task<VideoSourceProbe> ProbeAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var ffprobe = ResolveExecutable(_options.FfprobePath)
            ?? throw new InvalidOperationException("ffprobe is not available.");

        var (exit, stdout, stderr) = await RunAsync(ffprobe, FfmpegArgumentBuilder.Probe(sourcePath), cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException($"ffprobe failed ({exit}): {Tail(stderr)}");
        }

        int width = 0, height = 0;
        double duration = 0;
        var hasVideo = false;
        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq];
            var value = line[(eq + 1)..];
            switch (key)
            {
                case "width" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var w):
                    width = w;
                    hasVideo = true;
                    break;
                case "height" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h):
                    height = h;
                    break;
                case "duration" when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d):
                    duration = d;
                    break;
            }
        }

        return new VideoSourceProbe(duration, width, height, hasVideo);
    }

    public async Task<HeroRenditionSet> ProduceHeroRenditionsAsync(
        HeroTranscodeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var ffmpeg = ResolveExecutable(_options.FfmpegPath)
            ?? throw new InvalidOperationException("ffmpeg is not available.");

        Directory.CreateDirectory(request.OutputDirectory);

        var renditions = new List<HeroRenditionOutput>();
        foreach (var kind in Enum.GetValues<HeroRenditionKind>())
        {
            var outputPath = Path.Combine(request.OutputDirectory, FfmpegArgumentBuilder.OutputFileName(request.Stem, kind));
            await RunFfmpegAsync(ffmpeg, FfmpegArgumentBuilder.Rendition(kind, request, outputPath), cancellationToken);
            var (w, h) = FfmpegArgumentBuilder.Dimensions(kind);
            renditions.Add(new HeroRenditionOutput(kind, outputPath, w, h, new FileInfo(outputPath).Length));
        }

        var desktopPoster = Path.Combine(request.OutputDirectory, $"{request.Stem}-poster-desktop-frame.png");
        var mobilePoster = Path.Combine(request.OutputDirectory, $"{request.Stem}-poster-mobile-frame.png");
        await RunFfmpegAsync(ffmpeg, FfmpegArgumentBuilder.Poster(mobile: false, request, desktopPoster), cancellationToken);
        await RunFfmpegAsync(ffmpeg, FfmpegArgumentBuilder.Poster(mobile: true, request, mobilePoster), cancellationToken);

        return new HeroRenditionSet(renditions, desktopPoster, mobilePoster);
    }

    private async Task RunFfmpegAsync(string ffmpeg, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var (exit, _, stderr) = await RunAsync(ffmpeg, args, cancellationToken);
        if (exit != 0)
        {
            throw new InvalidOperationException($"ffmpeg failed ({exit}): {Tail(stderr)}");
        }
    }

    private async Task<(int Exit, string StdOut, string StdErr)> RunAsync(
        string executable, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Running {Executable} {Args}", executable, string.Join(' ', args));
        }

        using var process = new Process { StartInfo = psi };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) { stdout.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) { stderr.AppendLine(e.Data); } };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static string Tail(string text, int max = 500) =>
        text.Length <= max ? text.Trim() : text[^max..].Trim();

    // Resolve an executable: an existing path as-is, otherwise search PATH (adding .exe on Windows).
    private static string? ResolveExecutable(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath))
        {
            return null;
        }

        if (File.Exists(nameOrPath))
        {
            return nameOrPath;
        }

        var hasDir = nameOrPath.Contains('/') || nameOrPath.Contains('\\');
        if (hasDir)
        {
            return null; // a specific path was given but does not exist
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var candidates = OperatingSystem.IsWindows()
            ? new[] { nameOrPath, nameOrPath + ".exe" }
            : [nameOrPath];
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidate in candidates)
            {
                var full = Path.Combine(dir.Trim(), candidate);
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        return null;
    }
}
