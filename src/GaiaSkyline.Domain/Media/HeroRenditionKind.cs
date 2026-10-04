namespace GaiaSkyline.Domain.Media;

/// <summary>
/// The six hero-video renditions: desktop (landscape 16:9) and mobile (portrait 9:16), each in AV1 and
/// VP9 (WebM) and H.264 (MP4). The public markup offers them AV1 → VP9 → H.264 within each orientation
/// (ADR 0017 / Stage 7E-4).
/// </summary>
public enum HeroRenditionKind
{
    DesktopAv1,
    DesktopVp9,
    DesktopH264,
    MobileAv1,
    MobileVp9,
    MobileH264,
}

/// <summary>Orientation/codec helpers for <see cref="HeroRenditionKind"/>.</summary>
public static class HeroRenditionKindExtensions
{
    public static bool IsMobile(this HeroRenditionKind kind) => kind switch
    {
        HeroRenditionKind.MobileAv1 or HeroRenditionKind.MobileVp9 or HeroRenditionKind.MobileH264 => true,
        _ => false,
    };

    /// <summary>The HTTP content type for the rendition's container (WebM for AV1/VP9, MP4 for H.264).</summary>
    public static string ContentType(this HeroRenditionKind kind) => kind switch
    {
        HeroRenditionKind.DesktopH264 or HeroRenditionKind.MobileH264 => "video/mp4",
        _ => "video/webm",
    };
}
