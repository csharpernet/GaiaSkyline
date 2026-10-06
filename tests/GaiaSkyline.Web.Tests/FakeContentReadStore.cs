using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Domain.Stories;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// Deterministic <see cref="IContentReadStore"/> used to drive the HTTP content API tests without
/// a database: one text block (en + [DE] placeholder) and one number block (en only, so German
/// falls back), plus a single media asset.
/// </summary>
internal sealed class FakeContentReadStore : IContentReadStore
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly MediaAssetId KnownMediaId = MediaAssetId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private readonly List<ContentBlock> _blocks = Build();
    private readonly MediaAsset _asset = new(
        KnownMediaId, MediaKind.Image, "/media/known.svg", null, 1600, 1066, null, 100, "image/svg+xml", Now, "seed");

    private static List<ContentBlock> Build()
    {
        var headline = new ContentBlock(
            ContentBlockId.New(), "home.hero.headline", ContentKind.PlainText, "home", "Headline", 1, true, Now, "seed");
        headline.SetTranslation("en", "Wake up to the Dom Luís I Bridge", null, null, null, Now, "seed");
        headline.SetTranslation("de", "[DE] Wake up to the Dom Luís I Bridge", null, null, null, Now, "seed");

        var opacity = new ContentBlock(
            ContentBlockId.New(), "home.hero.overlay_opacity", ContentKind.Number, "home", "Opacity", 2, true, Now, "seed");
        opacity.SetTranslation("en", null, null, 0.35m, null, Now, "seed");

        return [headline, opacity];
    }

    public Task<IReadOnlyList<ContentBlock>> GetPublishedBlocksBySectionAsync(
        string section,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ContentBlock> result = _blocks.Where(b => b.Section == section && b.IsPublished).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ContentBlock>> GetAllBlocksBySectionAsync(
        string section,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ContentBlock> result = _blocks.Where(b => b.Section == section).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<MediaAssetId, MediaAsset>> GetMediaAssetsAsync(
        IReadOnlyCollection<MediaAssetId> ids,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<MediaAssetId, MediaAsset> result = new Dictionary<MediaAssetId, MediaAsset>();
        return Task.FromResult(result);
    }

    public Task<MediaAsset?> GetMediaAssetAsync(MediaAssetId id, CancellationToken cancellationToken) =>
        Task.FromResult(id == KnownMediaId ? _asset : null);

    public const string KnownReviewBody = "Good location, very easy to get into the center of Porto.";

    public Task<IReadOnlyList<Review>> GetPublishedReviewsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Review> result =
        [
            new Review(ReviewId.New(), 5, "Aicha", "Charlotte, North Carolina", KnownReviewBody, "Airbnb", new DateOnly(2026, 8, 1), true),
        ];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Story>> GetPublishedStoriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Story>>([]);

    public Task<Story?> GetPublishedStoryBySlugAsync(string slug, string language, CancellationToken cancellationToken) =>
        Task.FromResult<Story?>(null);

    public Task<GaiaSkyline.Domain.Entities.Property?> GetPropertyAsync(CancellationToken cancellationToken) =>
        Task.FromResult<GaiaSkyline.Domain.Entities.Property?>(null);

    public Task<MediaCollection?> GetMediaCollectionAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult<MediaCollection?>(null);
}
