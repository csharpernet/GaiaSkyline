namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// Shared naming for a hero video's staged source and its output renditions. The uploaded source is staged in
/// a temp folder (the web request writes it; the background job reads then deletes it). Each version's outputs
/// use a per-id stem so a new version never overwrites the live one's files (enabling the atomic swap).
/// </summary>
internal static class HeroVideoPaths
{
    public static string StagingDirectory => Path.Combine(Path.GetTempPath(), "gaia-hero");

    public static string SourcePath(Guid id, string extension)
    {
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return Path.Combine(StagingDirectory, $"{id:N}{ext}");
    }

    public static string? FindStagedSource(Guid id)
    {
        if (!Directory.Exists(StagingDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(StagingDirectory, $"{id:N}.*").FirstOrDefault();
    }

    public static string Stem(Guid id) => $"hero-{id:N}";

    public static string DesktopPosterStem(Guid id) => $"{Stem(id)}-poster-desktop";

    public static string MobilePosterStem(Guid id) => $"{Stem(id)}-poster-mobile";
}
