using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Application.Tests.Content;

public sealed class ContentServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeContentReadStore _store = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ContentRevision _revision = new();

    public void Dispose() => _cache.Dispose();

    private ContentService CreateService() => new(_store, _cache, _revision);

    private ContentBlock AddBlock(string key, ContentKind kind = ContentKind.PlainText, bool published = true)
    {
        var block = new ContentBlock(ContentBlockId.New(), key, kind, "home", key, 1, published, Now, "seed");
        _store.Blocks.Add(block);
        return block;
    }

    [Fact]
    public async Task Returns_value_in_the_requested_language()
    {
        var block = AddBlock("home.hero.headline");
        block.SetTranslation("en", "English", null, null, null, Now, "seed");
        block.SetTranslation("de", "Deutsch", null, null, null, Now, "seed");

        var payload = await CreateService().GetSectionAsync("home", "de", CancellationToken.None);

        payload.Items["home.hero.headline"].Text.Should().Be("Deutsch");
        payload.Items["home.hero.headline"].ResolvedLanguage.Should().Be("de");
    }

    [Fact]
    public async Task Falls_back_to_english_when_requested_language_is_missing()
    {
        var block = AddBlock("home.hero.headline");
        block.SetTranslation("en", "English only", null, null, null, Now, "seed");

        var payload = await CreateService().GetSectionAsync("home", "de", CancellationToken.None);

        payload.Items["home.hero.headline"].Text.Should().Be("English only");
        payload.Items["home.hero.headline"].ResolvedLanguage.Should().Be("en");
    }

    [Fact]
    public async Task Falls_back_to_english_when_requested_value_is_empty()
    {
        var block = AddBlock("home.hero.headline");
        block.SetTranslation("en", "English", null, null, null, Now, "seed");
        block.SetTranslation("de", "   ", null, null, null, Now, "seed"); // present but blank

        var payload = await CreateService().GetSectionAsync("home", "de", CancellationToken.None);

        payload.Items["home.hero.headline"].Text.Should().Be("English");
        payload.Items["home.hero.headline"].ResolvedLanguage.Should().Be("en");
    }

    [Fact]
    public async Task Returns_wrapped_key_when_missing_in_both_requested_and_english()
    {
        AddBlock("home.hero.headline");

        var payload = await CreateService().GetSectionAsync("home", "de", CancellationToken.None);

        payload.Items["home.hero.headline"].Text.Should().Be(ContentService.MissingValue("home.hero.headline"));
        payload.Items["home.hero.headline"].ResolvedLanguage.Should().Be("none");
    }

    [Fact]
    public async Task Resolves_number_and_boolean_kinds()
    {
        AddBlock("home.hero.overlay_opacity", ContentKind.Number)
            .SetTranslation("en", null, null, 0.35m, null, Now, "seed");
        AddBlock("amenities.outdoor.hot_tub", ContentKind.Boolean)
            .SetTranslation("en", null, null, null, true, Now, "seed");

        var payload = await CreateService().GetSectionAsync("home", "en", CancellationToken.None);

        payload.Items["home.hero.overlay_opacity"].Number.Should().Be(0.35m);
        payload.Items["amenities.outdoor.hot_tub"].Boolean.Should().BeTrue();
    }

    [Fact]
    public async Task Resolves_media_reference_to_a_dto()
    {
        var assetId = MediaAssetId.New();
        _store.Media[assetId] = new MediaAsset(
            assetId, MediaKind.Image, "/media/hero.jpg", null, 1600, 1066, null, 1000, "image/jpeg", Now, "seed");

        AddBlock("home.hero.poster", ContentKind.ImageRef)
            .SetTranslation("en", null, assetId, null, null, Now, "seed");

        var payload = await CreateService().GetSectionAsync("home", "en", CancellationToken.None);

        var value = payload.Items["home.hero.poster"];
        value.Media.Should().NotBeNull();
        value.Media!.BlobUri.Should().Be("/media/hero.jpg");
        value.Media.Id.Should().Be(assetId.Value);
    }

    [Fact]
    public async Task Excludes_unpublished_blocks()
    {
        AddBlock("home.published", published: true).SetTranslation("en", "Shown", null, null, null, Now, "seed");
        AddBlock("home.draft", published: false).SetTranslation("en", "Hidden", null, null, null, Now, "seed");

        var payload = await CreateService().GetSectionAsync("home", "en", CancellationToken.None);

        payload.Items.Should().ContainKey("home.published");
        payload.Items.Should().NotContainKey("home.draft");
    }

    [Fact]
    public async Task Caches_the_payload_until_the_revision_is_bumped()
    {
        var block = AddBlock("home.hero.headline");
        block.SetTranslation("en", "First", null, null, null, Now, "seed");
        var service = CreateService();

        var first = await service.GetSectionAsync("home", "en", CancellationToken.None);
        first.Items["home.hero.headline"].Text.Should().Be("First");
        _store.SectionQueryCount.Should().Be(1);

        // Mutate underlying data, but without a revision bump the cached payload is served.
        block.SetTranslation("en", "Second", null, null, null, Now, "seed");
        var cached = await service.GetSectionAsync("home", "en", CancellationToken.None);
        cached.Items["home.hero.headline"].Text.Should().Be("First");
        _store.SectionQueryCount.Should().Be(1);

        // Bumping the revision changes the cache key -> fresh read.
        _revision.Bump();
        var fresh = await service.GetSectionAsync("home", "en", CancellationToken.None);
        fresh.Items["home.hero.headline"].Text.Should().Be("Second");
        _store.SectionQueryCount.Should().Be(2);
    }

    [Fact]
    public async Task GetMediaAsset_returns_null_when_absent()
    {
        var result = await CreateService().GetMediaAssetAsync(MediaAssetId.New(), CancellationToken.None);

        result.Should().BeNull();
    }
}
