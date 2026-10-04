using ImageMagick;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// Encodes the AVIF rendition of a raster image via Magick.NET (ADR 0016). AVIF is the most efficient of
/// the three web formats, so it is offered first in the <c>&lt;picture&gt;</c> and capable browsers pick it.
/// Centralised here so the upload pipeline (<see cref="ImageRenditionService"/>) and the dev seeder emit
/// byte-compatible AVIF files with the same quality.
/// </summary>
internal static class AvifRaster
{
    /// <summary>The <c>{stem}-{width}.avif</c> extension the markup (ResponsiveImage) expects.</summary>
    public const string Extension = "avif";

    /// <summary>AVIF quality (0–100). ~50 roughly matches the quality-72 WebP/JPEG variants at a smaller size.</summary>
    public const int Quality = 50;

    /// <summary>Encodes already-encoded image bytes (e.g. a lossless PNG of the resized variant) to AVIF bytes.</summary>
    public static byte[] Encode(byte[] imageBytes, int quality = Quality)
    {
        using var image = new MagickImage(imageBytes);
        image.Quality = (uint)quality;
        image.Format = MagickFormat.Avif;
        return image.ToByteArray();
    }
}
