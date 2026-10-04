using System.Text;
using FluentAssertions;
using GaiaSkyline.Infrastructure.Media;
using ImageMagick;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class ImageRenditionServiceTests
{
    [Fact]
    public async Task Generates_avif_webp_jpeg_at_every_width_plus_lqip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gaia-rendition-" + Guid.NewGuid().ToString("N"));
        try
        {
            byte[] sourceBytes;
            using (var src = new MagickImage(MagickColors.CornflowerBlue, 1000, 600))
            {
                src.Format = MagickFormat.Png;
                sourceBytes = src.ToByteArray();
            }

            var service = new ImageRenditionService();
            var result = await service.GenerateAsync(
                new MemoryStream(sourceBytes), dir, "test", CancellationToken.None);

            result.Lqip.Should().StartWith("data:image/webp;base64,");
            result.Width.Should().Be(1600);
            result.MasterBytes.Should().BeGreaterThan(0);

            foreach (var w in new[] { 400, 800, 1600 })
            {
                File.Exists(Path.Combine(dir, $"test-{w}.jpg")).Should().BeTrue();
                File.Exists(Path.Combine(dir, $"test-{w}.webp")).Should().BeTrue();

                var avifPath = Path.Combine(dir, $"test-{w}.avif");
                File.Exists(avifPath).Should().BeTrue($"the AVIF rendition at {w}w should exist");
                IsAvif(avifPath).Should().BeTrue($"the {w}w file should carry the AVIF (ISOBMFF ftyp 'avif') signature");

                using var avif = new MagickImage(avifPath);
                avif.Width.Should().Be((uint)w);
                avif.Height.Should().BeGreaterThan(0);
            }
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // An AVIF file is ISOBMFF: bytes 4..8 are "ftyp" and the major/compatible brand "avif" appears in the box.
    private static bool IsAvif(string path)
    {
        var head = new byte[32];
        using (var fs = File.OpenRead(path))
        {
            var read = fs.Read(head, 0, head.Length);
            if (read < 12)
            {
                return false;
            }
        }

        var ascii = Encoding.ASCII.GetString(head);
        return ascii.Contains("ftyp", StringComparison.Ordinal) && ascii.Contains("avif", StringComparison.Ordinal);
    }
}
