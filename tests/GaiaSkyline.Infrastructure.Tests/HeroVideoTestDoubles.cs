using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Media;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>
/// A stand-in <see cref="IVideoTranscoder"/> for the hero-video service/job tests: no FFmpeg. It returns a
/// canned probe and, on produce, writes tiny placeholder rendition files plus two real PNG poster frames (so
/// the real image pipeline can process them). The real FFmpeg path is covered by ffprobe tests in 7E-4c.
/// </summary>
internal sealed class FakeVideoTranscoder(
    bool available = true, VideoSourceProbe? probe = null, bool throwOnProduce = false) : IVideoTranscoder
{
    private readonly VideoSourceProbe _probe = probe ?? new VideoSourceProbe(30, 1920, 1080, true);

    public bool IsAvailable { get; } = available;

    public int ProduceCount { get; private set; }

    public Task<VideoSourceProbe> ProbeAsync(string sourcePath, CancellationToken cancellationToken) =>
        Task.FromResult(_probe);

    public Task<HeroRenditionSet> ProduceHeroRenditionsAsync(HeroTranscodeRequest request, CancellationToken cancellationToken)
    {
        ProduceCount++;
        if (throwOnProduce)
        {
            throw new InvalidOperationException("transcode blew up");
        }

        Directory.CreateDirectory(request.OutputDirectory);

        var renditions = new List<HeroRenditionOutput>();
        foreach (var kind in Enum.GetValues<HeroRenditionKind>())
        {
            var path = Path.Combine(request.OutputDirectory, FfmpegArgumentBuilder.OutputFileName(request.Stem, kind));
            File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0x03]);
            var (w, h) = FfmpegArgumentBuilder.Dimensions(kind);
            renditions.Add(new HeroRenditionOutput(kind, path, w, h, new FileInfo(path).Length));
        }

        var desktopFrame = Path.Combine(request.OutputDirectory, $"{request.Stem}-poster-desktop-frame.png");
        var mobileFrame = Path.Combine(request.OutputDirectory, $"{request.Stem}-poster-mobile-frame.png");
        WritePng(desktopFrame, 1920, 1080);
        WritePng(mobileFrame, 720, 1280);

        return Task.FromResult(new HeroRenditionSet(renditions, desktopFrame, mobileFrame));
    }

    private static void WritePng(string path, int width, int height)
    {
        using var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.SteelBlue, (uint)width, (uint)height)
        {
            Format = ImageMagick.MagickFormat.Png,
        };
        image.Write(path);
    }
}

/// <summary>Captures the hero-video ids enqueued for transcoding.</summary>
internal sealed class RecordingHeroVideoJobScheduler : IHeroVideoJobScheduler
{
    public List<Guid> Enqueued { get; } = [];

    public void Enqueue(Guid heroVideoId) => Enqueued.Add(heroVideoId);
}

/// <summary>Points the transcode job at a fixed media directory (no web host).</summary>
internal sealed class FixedMediaDirectoryProvider(string directory) : IMediaDirectoryProvider
{
    public string GetMediaDirectory() => directory;
}
