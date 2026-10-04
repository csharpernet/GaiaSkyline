using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Media;

/// <summary>
/// A previous SEO filename (stem) of a <see cref="MediaAsset"/>, kept after a rename so the old — possibly
/// search-indexed or externally linked — image URL keeps working. The media URL middleware 301-redirects
/// <c>/media/{oldSlug}-{w}.{ext}</c> to the asset's current filename. Content references point at the asset
/// id, not the URL, so they survive a rename on their own; this row is only for external URLs (Stage 7E-2d).
/// </summary>
public sealed class MediaAssetAlias : Entity<MediaAssetAliasId>
{
    // Required by EF Core's materialization.
    private MediaAssetAlias()
    {
    }

    public MediaAssetAlias(MediaAssetAliasId id, MediaAssetId mediaAssetId, string oldSlug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldSlug);

        Id = id;
        MediaAssetId = mediaAssetId;
        OldSlug = oldSlug.Trim();
    }

    public MediaAssetId MediaAssetId { get; private set; }

    /// <summary>The vacated filename stem (no path, no <c>-{w}.{ext}</c> suffix), e.g. <c>douro-balcony</c>.</summary>
    public string OldSlug { get; private set; } = null!;
}
