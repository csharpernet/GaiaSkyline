using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace GaiaSkyline.Infrastructure.Media;

public sealed record RenditionResult(string Lqip, long MasterBytes, int Width, int Height);

/// <summary>
/// Generates the responsive raster set (WebP + JPEG at 400/800/1600w) plus a tiny LQIP, using the same
/// encoder settings and the <c>{stem}-{w}.{ext}</c> naming the dev seeder and ResponsiveImage rely on.
/// The 1600w JPEG is the canonical master (<c>{stem}-1600.jpg</c>).
/// </summary>
internal sealed class ImageRenditionService : IImageRenditionService
{
    private static readonly int[] Widths = [400, 800, 1600];
    private const int MasterWidth = 1600;

    public async Task<RenditionResult> GenerateAsync(
        Stream source, string destinationDirectory, string stem, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationDirectory);

        using var master = await Image.LoadAsync<Rgba32>(source, cancellationToken);
        var jpeg = new JpegEncoder { Quality = 72 };
        var webp = new WebpEncoder { Quality = 72, FileFormat = WebpFileFormatType.Lossy };

        long masterBytes = 0;
        var masterHeight = master.Height;

        foreach (var width in Widths)
        {
            var height = (int)Math.Round((double)master.Height * width / master.Width);
            using var variant = master.Clone(x => x.Resize(width, height));

            var jpgPath = Path.Combine(destinationDirectory, $"{stem}-{width}.jpg");
            var webpPath = Path.Combine(destinationDirectory, $"{stem}-{width}.webp");
            await variant.SaveAsync(jpgPath, jpeg, cancellationToken);
            await variant.SaveAsync(webpPath, webp, cancellationToken);

            if (width == MasterWidth)
            {
                masterBytes = new FileInfo(jpgPath).Length;
                masterHeight = height;
            }
        }

        var lqipHeight = Math.Max(1, (int)Math.Round(24.0 * master.Height / master.Width));
        using var tiny = master.Clone(x => x.Resize(24, lqipHeight));
        using var buffer = new MemoryStream();
        await tiny.SaveAsync(buffer, new WebpEncoder { Quality = 40, FileFormat = WebpFileFormatType.Lossy }, cancellationToken);
        var lqip = $"data:image/webp;base64,{Convert.ToBase64String(buffer.ToArray())}";

        return new RenditionResult(lqip, masterBytes, MasterWidth, masterHeight);
    }
}

/// <summary>Produces the responsive raster set for an uploaded image.</summary>
public interface IImageRenditionService
{
    Task<RenditionResult> GenerateAsync(Stream source, string destinationDirectory, string stem, CancellationToken cancellationToken);
}
