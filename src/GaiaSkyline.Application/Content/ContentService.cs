using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Application.Content;

/// <summary>
/// Resolves a section's published content for a language with an English fallback, and caches the
/// result keyed by (section, language, content revision) with a 10-minute safety-net TTL.
/// </summary>
public sealed class ContentService : IContentService
{
    public const string DefaultLanguage = "en";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private static readonly IReadOnlyDictionary<MediaAssetId, MediaAsset> NoMedia =
        new Dictionary<MediaAssetId, MediaAsset>();

    private readonly IContentReadStore _readStore;
    private readonly IMemoryCache _cache;
    private readonly IContentRevision _revision;

    public ContentService(IContentReadStore readStore, IMemoryCache cache, IContentRevision revision)
    {
        ArgumentNullException.ThrowIfNull(readStore);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(revision);

        _readStore = readStore;
        _cache = cache;
        _revision = revision;
    }

    public async Task<ContentPayload> GetSectionAsync(
        string section,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        var lang = string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language.Trim();
        var cacheKey = $"content::{section}::{lang}::{_revision.Current}";

        if (_cache.TryGetValue(cacheKey, out ContentPayload? cached) && cached is not null)
        {
            return cached;
        }

        var payload = await BuildPayloadAsync(section, lang, cancellationToken);
        _cache.Set(cacheKey, payload, CacheTtl);
        return payload;
    }

    public async Task<MediaAssetDto?> GetMediaAssetAsync(MediaAssetId id, CancellationToken cancellationToken)
    {
        var asset = await _readStore.GetMediaAssetAsync(id, cancellationToken);
        return asset is null ? null : ToDto(asset);
    }

    /// <summary>The visible gap marker used when a value is missing in both the requested language and English.</summary>
    public static string MissingValue(string key) => $"‹{key}›";

    private async Task<ContentPayload> BuildPayloadAsync(
        string section,
        string language,
        CancellationToken cancellationToken)
    {
        var blocks = await _readStore.GetPublishedBlocksBySectionAsync(section, cancellationToken);

        var mediaIds = blocks
            .SelectMany(b => b.Translations)
            .Where(t => t.ValueMediaAssetId.HasValue)
            .Select(t => t.ValueMediaAssetId!.Value)
            .Distinct()
            .ToList();

        var media = mediaIds.Count == 0
            ? NoMedia
            : await _readStore.GetMediaAssetsAsync(mediaIds, cancellationToken);

        var items = new Dictionary<string, ContentValue>(StringComparer.Ordinal);
        foreach (var block in blocks
            .OrderBy(b => b.DisplayOrder)
            .ThenBy(b => b.Key, StringComparer.Ordinal))
        {
            items[block.Key] = Resolve(block, language, media);
        }

        return new ContentPayload { Section = section, Language = language, Items = items };
    }

    /// <summary>
    /// Resolve one block for a language: requested language → English → ‹key› gap marker.
    /// Pure and side-effect free so it is unit-testable without a database.
    /// </summary>
    internal static ContentValue Resolve(
        ContentBlock block,
        string language,
        IReadOnlyDictionary<MediaAssetId, MediaAsset> media)
    {
        var picked = PickTranslation(block, language);
        if (picked is null)
        {
            return new ContentValue
            {
                Kind = block.Kind,
                Text = MissingValue(block.Key),
                ResolvedLanguage = "none",
            };
        }

        var (translation, resolvedLanguage) = picked.Value;

        // Surface every value the chosen translation carries. Kind tells the consumer which is
        // primary, but a block may legitimately carry more than one (e.g. an amenity has a text
        // label AND a boolean availability flag).
        return new ContentValue
        {
            Kind = block.Kind,
            Text = string.IsNullOrWhiteSpace(translation.ValueText) ? null : translation.ValueText,
            Number = translation.ValueNumber,
            Boolean = translation.ValueBoolean,
            Media = ResolveMedia(translation, media),
            ResolvedLanguage = resolvedLanguage,
        };
    }

    private static (ContentTranslation Translation, string Language)? PickTranslation(
        ContentBlock block,
        string language)
    {
        var requested = Find(block, language);
        if (HasValue(block.Kind, requested))
        {
            return (requested!, language);
        }

        if (!string.Equals(language, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            var english = Find(block, DefaultLanguage);
            if (HasValue(block.Kind, english))
            {
                return (english!, DefaultLanguage);
            }
        }

        return null;
    }

    private static ContentTranslation? Find(ContentBlock block, string language) =>
        block.Translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase));

    private static bool HasValue(ContentKind kind, ContentTranslation? translation)
    {
        if (translation is null)
        {
            return false;
        }

        return kind switch
        {
            ContentKind.Number => translation.ValueNumber.HasValue,
            ContentKind.Boolean => translation.ValueBoolean.HasValue,
            ContentKind.ImageRef or ContentKind.VideoRef => translation.ValueMediaAssetId.HasValue,
            _ => !string.IsNullOrWhiteSpace(translation.ValueText),
        };
    }

    private static MediaAssetDto? ResolveMedia(
        ContentTranslation translation,
        IReadOnlyDictionary<MediaAssetId, MediaAsset> media) =>
        translation.ValueMediaAssetId is { } id && media.TryGetValue(id, out var asset)
            ? ToDto(asset)
            : null;

    private static MediaAssetDto ToDto(MediaAsset asset) => new(
        asset.Id.Value,
        asset.Kind,
        asset.BlobUri,
        asset.PosterBlobUri,
        asset.Width,
        asset.Height,
        asset.DurationSec,
        asset.ByteSize,
        asset.ContentType);
}
