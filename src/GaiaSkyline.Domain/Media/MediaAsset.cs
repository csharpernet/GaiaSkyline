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
    /// Legacy single alt text, kept as the English/default fallback. Per-language alt lives in
    /// <see cref="AltTexts"/>; the public site resolves the request language → en → this value.
    /// </summary>
    public string? AltText { get; private set; }

    /// <summary>Per-language alt text (Stage 7E). Required for every enabled language before public use.</summary>
    public IReadOnlyCollection<MediaAssetAltText> AltTexts => _altTexts.AsReadOnly();

    /// <summary>
    /// Low-Quality Image Placeholder: a tiny blurred preview as a self-contained <c>data:</c> URI,
    /// shown behind the real image while it loads to cut perceived LCP without a layout shift.
    /// </summary>
    public string? Lqip { get; private set; }

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

