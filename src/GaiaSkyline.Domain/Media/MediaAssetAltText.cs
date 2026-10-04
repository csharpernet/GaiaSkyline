using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Media;

/// <summary>
/// One language's alt text for a <see cref="MediaAsset"/>. Alt text is authored per language because the
/// public site requires it before an image may be used on a page (ADR 0008 / Stage 7E); the asset's legacy
/// single <see cref="MediaAsset.AltText"/> remains the English fallback.
/// </summary>
public sealed class MediaAssetAltText : Entity<MediaAssetAltTextId>
{
    // Required by EF Core's materialization.
    private MediaAssetAltText()
    {
    }

    public MediaAssetAltText(MediaAssetAltTextId id, MediaAssetId mediaAssetId, string languageCode, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        Id = id;
        MediaAssetId = mediaAssetId;
        LanguageCode = languageCode.Trim();
        Text = text.Trim();
    }

    public MediaAssetId MediaAssetId { get; private set; }

    /// <summary>BCP-47 tag, stored as-is (e.g. "en", "pt-PT").</summary>
    public string LanguageCode { get; private set; } = null!;

    public string Text { get; private set; } = null!;

    internal void Update(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text.Trim();
    }
}
