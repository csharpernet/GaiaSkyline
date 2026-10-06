using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Media;

/// <summary>
/// An image or video. <see cref="BlobUri"/> is a relative path under wwwroot/media in dev and an
/// Azure Blob URL in production (swapped behind <c>IMediaStorage</c>; see ADR 0006).
/// </summary>
public sealed class MediaAsset : Entity<MediaAssetId>
{
    private readonly List<MediaAssetAltText> _altTexts = [];
    private readonly List<MediaAssetAlias> _aliases = [];

    // Required by EF Core's materialization.
    private MediaAsset()
    {
    }

    public MediaAsset(
        MediaAssetId id,
        MediaKind kind,
        string blobUri,
        string? posterBlobUri,
        int width,
        int height,
        int? durationSec,
        long byteSize,
        string contentType,
        DateTime uploadedAtUtc,
        string uploadedBy,
        string? altText = null,
        string? lqip = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(byteSize);

        Id = id;
        Kind = kind;
        BlobUri = blobUri.Trim();
        PosterBlobUri = string.IsNullOrWhiteSpace(posterBlobUri) ? null : posterBlobUri.Trim();
        Width = width;
        Height = height;
        DurationSec = durationSec;
        ByteSize = byteSize;
        ContentType = contentType.Trim();
        UploadedAtUtc = uploadedAtUtc;
        UploadedBy = uploadedBy;
        AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        Lqip = string.IsNullOrWhiteSpace(lqip) ? null : lqip.Trim();
    }

    public MediaKind Kind { get; private set; }

    public string BlobUri { get; private set; } = null!;

    public string? PosterBlobUri { get; private set; }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int? DurationSec { get; private set; }

    public long ByteSize { get; private set; }

    public string ContentType { get; private set; } = null!;

    public DateTime UploadedAtUtc { get; private set; }

    public string UploadedBy { get; private set; } = null!;

    /// <summary>
    /// Cache-busting version (Stage 8 Part B): starts at 1 and bumps whenever the binary is replaced.
    /// Public URLs carry it as <c>?v=</c>, so browsers and the CDN refetch replaced files while unchanged
    /// ones keep long-lived immutable caching. The SEO filename never changes for a replace.
    /// </summary>
    public int Version { get; private set; } = 1;

    /// <summary>
    /// Legacy single alt text, kept as the English/default fallback. Per-language alt lives in
    /// <see cref="AltTexts"/>; the public site resolves the request language → en → this value.
    /// </summary>
    public string? AltText { get; private set; }

    /// <summary>Per-language alt text (Stage 7E). Required for every enabled language before public use.</summary>
    public IReadOnlyCollection<MediaAssetAltText> AltTexts => _altTexts.AsReadOnly();

    /// <summary>
    /// Previous SEO filenames kept alive after a rename (Stage 7E-2d). The media URL middleware 301-redirects
    /// each old stem to the current <see cref="BlobUri"/> so indexed/linked image URLs never break.
    /// </summary>
    public IReadOnlyCollection<MediaAssetAlias> Aliases => _aliases.AsReadOnly();

    /// <summary>
    /// Low-Quality Image Placeholder: a tiny blurred preview as a self-contained <c>data:</c> URI,
    /// shown behind the real image while it loads to cut perceived LCP without a layout shift.
    /// </summary>
    public string? Lqip { get; private set; }

    /// <summary>Soft delete: the row and its renditions stay (so references survive) but it is hidden from
    /// the library and the pickers. Only unused assets may be deleted; it can be restored.</summary>
    public bool IsDeleted { get; private set; }

    public DateTime? DeletedAtUtc { get; private set; }

    public void SoftDelete(DateTime utcNow)
    {
        IsDeleted = true;
        DeletedAtUtc = utcNow;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAtUtc = null;
    }

    /// <summary>
    /// Replace the binary while keeping the id and <see cref="BlobUri"/> (the renditions are regenerated at the
    /// same stem), so every content/collection reference survives. Alt text and kind are unchanged.
    /// </summary>
    public void ReplaceRenditions(int width, int height, long byteSize, string? lqip, DateTime uploadedAtUtc, string uploadedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentOutOfRangeException.ThrowIfNegative(byteSize);

        Width = width;
        Height = height;
        ByteSize = byteSize;
        Lqip = string.IsNullOrWhiteSpace(lqip) ? null : lqip.Trim();
        UploadedAtUtc = uploadedAtUtc;
        UploadedBy = uploadedBy;
        Version++; // same URL stem, new bytes → bust browser + CDN caches (Stage 8 Part B)
    }

    /// <summary>
    /// Give the asset a new SEO filename (Stage 7E-2d). The id is unchanged, so every content/collection
    /// reference (they point at the id) survives. The caller moves the on-disk renditions to the new stem and
    /// passes the rebuilt <paramref name="newBlobUri"/>. The vacated stem is remembered as an alias so its old
    /// URL can 301 to the new one; reclaiming a stem we previously aliased drops that alias (it is a live file
    /// again).
    /// </summary>
    public void Rename(string newBlobUri, string previousStem, string newStem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newBlobUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(previousStem);
        ArgumentException.ThrowIfNullOrWhiteSpace(newStem);

        BlobUri = newBlobUri.Trim();

        if (!string.Equals(previousStem, newStem, StringComparison.OrdinalIgnoreCase)
            && !_aliases.Any(a => string.Equals(a.OldSlug, previousStem, StringComparison.OrdinalIgnoreCase)))
        {
            _aliases.Add(new MediaAssetAlias(MediaAssetAliasId.New(), Id, previousStem));
        }

        var reclaimed = _aliases.FirstOrDefault(
            a => string.Equals(a.OldSlug, newStem, StringComparison.OrdinalIgnoreCase));
        if (reclaimed is not null)
        {
            _aliases.Remove(reclaimed);
        }
    }

    /// <summary>Insert, update or (when <paramref name="text"/> is blank) remove a language's alt text.</summary>
    public void SetAltText(string languageCode, string? text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        var lang = languageCode.Trim();
        var existing = _altTexts.FirstOrDefault(
            a => string.Equals(a.LanguageCode, lang, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(text))
        {
            if (existing is not null)
            {
                _altTexts.Remove(existing);
            }

            return;
        }

        if (existing is not null)
        {
            existing.Update(text);
        }
        else
        {
            _altTexts.Add(new MediaAssetAltText(MediaAssetAltTextId.New(), Id, lang, text));
        }
    }

    /// <summary>The alt text to show for a language: that language → English → the legacy single value → null.</summary>
    public string? AltTextFor(string languageCode)
    {
        var hit = Explicit(languageCode);
        if (!string.IsNullOrWhiteSpace(hit))
        {
            return hit;
        }

        var english = Explicit("en");
        if (!string.IsNullOrWhiteSpace(english))
        {
            return english;
        }

        return string.IsNullOrWhiteSpace(AltText) ? null : AltText;
    }

    /// <summary>
    /// True only when every requested language has its own non-empty alt text (no fallback). This is the
    /// gate the admin enforces before an image may be placed on a public page.
    /// </summary>
    public bool HasExplicitAltTextForAllLanguages(IEnumerable<string> languageCodes)
    {
        ArgumentNullException.ThrowIfNull(languageCodes);
        return languageCodes.All(l => !string.IsNullOrWhiteSpace(Explicit(l)));
    }

    private string? Explicit(string languageCode) =>
        _altTexts.FirstOrDefault(
            a => string.Equals(a.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))?.Text;
}

