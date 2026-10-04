using System.Globalization;
using GaiaSkyline.Application.Content;

namespace GaiaSkyline.Web.Media;

/// <summary>
/// View model for the shared <c>_Picture</c> partial. Builds the AVIF + WebP + JPEG <c>srcset</c> for a
/// media asset produced by the responsive raster pipeline (see ADR 0008 and ADR 0016). AVIF is offered
/// first so capable browsers pick the smallest format.
/// </summary>
public sealed record ResponsiveImage
{
    private static readonly int[] Widths = [400, 800, 1600];
    private const string MasterSuffix = "-1600.jpg";

    private ResponsiveImage(
        string src,
        string? avifSrcset,
        string? webpSrcset,
        string? jpegSrcset,
        string sizes,
        int width,
        int height,
        string alt,
        string? lqip,
        bool eager,
        bool highPriority,
        string wrapperClass)
    {
        Src = src;
        AvifSrcset = avifSrcset;
        WebpSrcset = webpSrcset;
        JpegSrcset = jpegSrcset;
        Sizes = sizes;
        Width = width;
        Height = height;
        Alt = alt;
        Lqip = lqip;
        Eager = eager;
        HighPriority = highPriority;
        WrapperClass = wrapperClass;
    }

    /// <summary>Fallback source: the full-width JPEG.</summary>
    public string Src { get; }

    public string? AvifSrcset { get; }

    public string? WebpSrcset { get; }

    public string? JpegSrcset { get; }

    /// <summary>The <c>sizes</c> attribute describing the rendered width at each breakpoint.</summary>
    public string Sizes { get; }

    public int Width { get; }

    public int Height { get; }

    public string Alt { get; }

    public string? Lqip { get; }

    public bool Eager { get; }

    public bool HighPriority { get; }

    /// <summary>
    /// Extra classes for the wrapper. Must include an aspect-ratio utility (e.g. <c>aspect-[3/2]</c>)
    /// because the image fills the wrapper absolutely — that reserves the box and keeps CLS at zero.
    /// </summary>
    public string WrapperClass { get; }

    /// <summary>
    /// Builds a <see cref="ResponsiveImage"/> for an asset. <paramref name="sizes"/> is the CSS
    /// <c>sizes</c> hint; <paramref name="wrapperClass"/> sets the aspect-ratio box and any rounding.
    /// </summary>
    public static ResponsiveImage From(
        MediaAssetDto asset,
        string sizes,
        string wrapperClass,
        bool eager = false,
        bool highPriority = false)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var stem = Stem(asset.BlobUri);
        string? avif = null;
        string? webp = null;
        string? jpeg = null;
        if (stem is not null)
        {
            avif = string.Join(", ", Widths.Select(w => string.Create(
                CultureInfo.InvariantCulture, $"{stem}-{w}.avif {w}w")));
            webp = string.Join(", ", Widths.Select(w => string.Create(
                CultureInfo.InvariantCulture, $"{stem}-{w}.webp {w}w")));
            jpeg = string.Join(", ", Widths.Select(w => string.Create(
                CultureInfo.InvariantCulture, $"{stem}-{w}.jpg {w}w")));
        }

        return new ResponsiveImage(
            asset.BlobUri,
            avif,
            webp,
            jpeg,
            sizes,
            asset.Width,
            asset.Height,
            ResolveAlt(asset),
            asset.Lqip,
            eager,
            highPriority,
            wrapperClass);
    }

    /// <summary>Alt for the current request culture: that language → English → the legacy single value → "".</summary>
    private static string ResolveAlt(MediaAssetDto asset)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        if (asset.AltByLang.TryGetValue(culture, out var exact) && !string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        if (asset.AltByLang.TryGetValue("en", out var english) && !string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        return asset.Alt ?? string.Empty;
    }

    // Pipeline rasters are named "{stem}-1600.jpg"; only those have sibling variants to offer.
    private static string? Stem(string blobUri) =>
        blobUri.EndsWith(MasterSuffix, StringComparison.OrdinalIgnoreCase)
            ? blobUri[..^MasterSuffix.Length]
            : null;
}
