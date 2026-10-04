namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// The responsive raster set is named <c>{stem}-{w}.{ext}</c> with the 1600w JPEG as the canonical master
/// (<c>{stem}-1600.jpg</c>, pointed at by <c>MediaAsset.BlobUri</c>). This recovers the stem from a blob URI
/// — works for a dev relative path and a prod absolute URL alike, since it reads only the file name.
/// </summary>
internal static class MediaStem
{
    public const string MasterSuffix = "-1600.jpg";

    public static string? Of(string? blobUri)
    {
        if (string.IsNullOrWhiteSpace(blobUri))
        {
            return null;
        }

        var fileName = Path.GetFileName(blobUri);
        return fileName.EndsWith(MasterSuffix, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^MasterSuffix.Length]
            : null;
    }
}
