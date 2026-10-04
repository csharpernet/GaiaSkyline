using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Domain.Stories;
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
    private readonly IContentPreviewState _preview;

    public ContentService(
        IContentReadStore readStore,
        IMemoryCache cache,
        IContentRevision revision,
        IContentPreviewState? preview = null)
    {
        ArgumentNullException.ThrowIfNull(readStore);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(revision);

        _readStore = readStore;
        _cache = cache;
        _revision = revision;
        _preview = preview ?? new NoPreviewState();
    }

    public async Task<ContentPayload> GetSectionAsync(
        string section,
        string language,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);

        var lang = string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language.Trim();

        // Owner preview: show draft values and unpublished blocks, and never cache (the public
        // output cache is bypassed separately when the preview cookie is present).
        if (_preview.IsPreview)
        {
            return await BuildPayloadAsync(section, lang, preview: true, cancellationToken);
        }

        var cacheKey = $"content::{section}::{lang}::{_revision.Current}";
        if (_cache.TryGetValue(cacheKey, out ContentPayload? cached) && cached is not null)
        {
            return cached;
        }

        var payload = await BuildPayloadAsync(section, lang, preview: false, cancellationToken);
        _cache.Set(cacheKey, payload, CacheTtl);
        return payload;
    }

    public async Task<MediaAssetDto?> GetMediaAssetAsync(MediaAssetId id, CancellationToken cancellationToken)
    {
        var asset = await _readStore.GetMediaAssetAsync(id, cancellationToken);
        return asset is null ? null : ToDto(asset);
    }

    public async Task<IReadOnlyList<ReviewDto>> GetPublishedReviewsAsync(CancellationToken cancellationToken)
    {
        var reviews = await _readStore.GetPublishedReviewsAsync(cancellationToken);
        return reviews.Select(ToReviewDto).ToList();
    }

    public async Task<IReadOnlyList<StoryDto>> GetPublishedStoriesAsync(
        string language,
        int? take,
        CancellationToken cancellationToken)
    {
        var lang = Normalize(language);
        var stories = await _readStore.GetPublishedStoriesAsync(cancellationToken);
        if (take is int limit)
        {
            stories = stories.Take(limit).ToList();
        }

        var coverIds = stories.Select(s => s.CoverMediaAssetId).Distinct().ToList();
        var media = coverIds.Count == 0 ? NoMedia : await _readStore.GetMediaAssetsAsync(coverIds, cancellationToken);

        return stories.Select(s => ToStoryDto(s, lang, media)).ToList();
    }

    public async Task<StoryDto?> GetStoryAsync(string slug, string language, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var story = await _readStore.GetPublishedStoryBySlugAsync(slug.Trim().ToLowerInvariant(), cancellationToken);
        if (story is null)
        {
            return null;
        }

        var media = await _readStore.GetMediaAssetsAsync([story.CoverMediaAssetId], cancellationToken);
        return ToStoryDto(story, Normalize(language), media);
    }

    public async Task<PropertyDto?> GetPropertyAsync(CancellationToken cancellationToken)
    {
        var property = await _readStore.GetPropertyAsync(cancellationToken);
        return property is null ? null : ToPropertyDto(property);
    }

    public async Task<IReadOnlyList<GalleryImageDto>> GetGalleryAsync(string key, CancellationToken cancellationToken)
    {
        var collection = await _readStore.GetMediaCollectionAsync(key, cancellationToken);
        if (collection is null)
        {
            return [];
        }

        var ids = collection.Items.Select(i => i.MediaAssetId).Distinct().ToList();
        var media = ids.Count == 0 ? NoMedia : await _readStore.GetMediaAssetsAsync(ids, cancellationToken);

        return collection.Items
            .Where(i => media.ContainsKey(i.MediaAssetId))
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new GalleryImageDto(ToDto(media[i.MediaAssetId]), i.DisplayOrder, i.IsHero))
            .ToList();
    }

    /// <summary>The visible gap marker used when a value is missing in both the requested language and English.</summary>
    public static string MissingValue(string key) => $"‹{key}›";

    private async Task<ContentPayload> BuildPayloadAsync(
        string section,
        string language,
        bool preview,
        CancellationToken cancellationToken)
    {
        var blocks = preview
            ? await _readStore.GetAllBlocksBySectionAsync(section, cancellationToken)
            : await _readStore.GetPublishedBlocksBySectionAsync(section, cancellationToken);

        var mediaIds = blocks
            .SelectMany(b => b.Translations)
            .Select(t => preview ? t.EffectiveMediaAssetId : t.ValueMediaAssetId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
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
            items[block.Key] = Resolve(block, language, media, preview);
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
        IReadOnlyDictionary<MediaAssetId, MediaAsset> media,
        bool preview = false)
    {
        var picked = PickTranslation(block, language, preview);
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

        // In preview, read the effective (draft-or-published) values; publicly, the published values.
        // Surface every value the chosen translation carries — a block may carry more than one.
        var text = preview ? translation.EffectiveText : translation.ValueText;
        return new ContentValue
        {
            Kind = block.Kind,
            Text = string.IsNullOrWhiteSpace(text) ? null : text,
            Number = preview ? translation.EffectiveNumber : translation.ValueNumber,
            Boolean = preview ? translation.EffectiveBoolean : translation.ValueBoolean,
            Media = ResolveMedia(translation, media, preview),
            ResolvedLanguage = resolvedLanguage,
        };
    }

    private static (ContentTranslation Translation, string Language)? PickTranslation(
        ContentBlock block,
        string language,
        bool preview)
    {
        var requested = Find(block, language);
        if (HasValue(block.Kind, requested, preview))
        {
            return (requested!, language);
        }

        if (!string.Equals(language, DefaultLanguage, StringComparison.OrdinalIgnoreCase))
        {
            var english = Find(block, DefaultLanguage);
            if (HasValue(block.Kind, english, preview))
            {
                return (english!, DefaultLanguage);
            }
        }

        return null;
    }

    private static ContentTranslation? Find(ContentBlock block, string language) =>
        block.Translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase));

    private static bool HasValue(ContentKind kind, ContentTranslation? translation, bool preview)
    {
        if (translation is null)
        {
            return false;
        }

        return kind switch
        {
            ContentKind.Number => (preview ? translation.EffectiveNumber : translation.ValueNumber).HasValue,
            ContentKind.Boolean => (preview ? translation.EffectiveBoolean : translation.ValueBoolean).HasValue,
            ContentKind.ImageRef or ContentKind.VideoRef => (preview ? translation.EffectiveMediaAssetId : translation.ValueMediaAssetId).HasValue,
            _ => !string.IsNullOrWhiteSpace(preview ? translation.EffectiveText : translation.ValueText),
        };
    }

    private static MediaAssetDto? ResolveMedia(
        ContentTranslation translation,
        IReadOnlyDictionary<MediaAssetId, MediaAsset> media,
        bool preview) =>
        (preview ? translation.EffectiveMediaAssetId : translation.ValueMediaAssetId) is { } id && media.TryGetValue(id, out var asset)
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
        asset.ContentType,
        asset.AltText,
        asset.Lqip);

    private static ReviewDto ToReviewDto(Review review) => new(
        review.Id.Value,
        review.Rating,
        review.GuestFirstName,
        review.GuestLocation,
        review.Body,
        review.Source,
        review.StayedOn);

    private static string Normalize(string language) =>
        string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language.Trim();

    private static StoryDto ToStoryDto(
        Story story,
        string language,
        IReadOnlyDictionary<MediaAssetId, MediaAsset> media)
    {
        var translation = FindStoryTranslation(story, language)
            ?? (string.Equals(language, DefaultLanguage, StringComparison.OrdinalIgnoreCase)
                ? null
                : FindStoryTranslation(story, DefaultLanguage));

        var cover = media.TryGetValue(story.CoverMediaAssetId, out var asset) ? ToDto(asset) : null;

        return new StoryDto(
            story.Slug,
            translation?.Title ?? MissingValue(story.Slug),
            translation?.Excerpt ?? string.Empty,
            translation?.BodyRichText ?? string.Empty,
            translation?.MetaTitle,
            translation?.MetaDescription,
            cover,
            story.PublishedAtUtc,
            translation?.ReadingTimeMinutes ?? 0,
            story.AuthorName,
            translation?.LanguageCode ?? "none");
    }

    private static StoryTranslation? FindStoryTranslation(Story story, string language) =>
        story.Translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase));

    private static PropertyDto ToPropertyDto(Property property) => new(
        property.Name,
        property.RegistrationCode,
        property.Address,
        property.Lat,
        property.Lng,
        property.DefaultCurrency,
        property.Timezone,
        property.CheckInFromLocal,
        property.CheckOutByLocal,
        property.Sleeps,
        property.Bedrooms,
        property.Beds,
        property.Bathrooms,
        property.BedsBreakdown);
}
