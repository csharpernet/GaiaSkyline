using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Content;
using GaiaSkyline.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class ContentSeedingTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>, IDisposable
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly LocalDbFixture _fixture = fixture;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ContentRevision _revision = new();

    public void Dispose() => _cache.Dispose();

    private async Task SeedAsync()
    {
        await using var dbContext = _fixture.CreateContext();
        var seeder = new ContentSeeder(dbContext, _revision);
        await seeder.SeedAsync(mediaPhysicalRoot: null, CancellationToken.None);
    }

    private async Task<ContentPayload> GetSectionAsync(string section, string language)
    {
        await using var dbContext = _fixture.CreateContext();
        var service = new ContentService(new ContentReadStore(dbContext), _cache, _revision);
        return await service.GetSectionAsync(section, language, CancellationToken.None);
    }

    private async Task<IReadOnlyList<ReviewDto>> GetReviewsAsync()
    {
        await using var dbContext = _fixture.CreateContext();
        var service = new ContentService(new ContentReadStore(dbContext), _cache, _revision);
        return await service.GetPublishedReviewsAsync(CancellationToken.None);
    }

    private async Task<Property?> GetPropertyAsync()
    {
        await using var dbContext = _fixture.CreateContext();
        return await dbContext.Properties.AsNoTracking().FirstOrDefaultAsync();
    }

    private async Task<int[]> RowCountsAsync()
    {
        await using var dbContext = _fixture.CreateContext();
        return
        [
            await dbContext.Properties.CountAsync(),
            await dbContext.ContentBlocks.CountAsync(),
            await dbContext.ContentTranslations.CountAsync(),
            await dbContext.MediaAssets.CountAsync(),
            await dbContext.MediaCollections.CountAsync(),
            await dbContext.MediaCollectionItems.CountAsync(),
            await dbContext.Reviews.CountAsync(),
        ];
    }

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        await SeedAsync();
        var afterFirst = await RowCountsAsync();

        await SeedAsync();
        var afterSecond = await RowCountsAsync();

        afterSecond.Should().Equal(afterFirst);
        afterFirst.Sum().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task English_home_payload_contains_the_authoritative_text()
    {
        await SeedAsync();

        var payload = await GetSectionAsync("home", "en");

        payload.Items.Should().ContainKey("home.hero.headline");
        payload.Items["home.hero.headline"].Text.Should().Be("Wake up to the Dom Luís I Bridge");
        payload.Items["home.hero.headline"].ResolvedLanguage.Should().Be("en");
        payload.Items.Values.Should().OnlyContain(v => v.ResolvedLanguage == "en");
    }

    [Fact]
    public async Task German_text_blocks_are_placeholders_but_numbers_fall_back_to_english()
    {
        await SeedAsync();

        var payload = await GetSectionAsync("home", "de");

        // Text block: German placeholder present.
        var headline = payload.Items["home.hero.headline"];
        headline.ResolvedLanguage.Should().Be("de");
        headline.Text.Should().Be("[DE] Wake up to the Dom Luís I Bridge");

        // Number block: no German translation seeded, so it falls back to English.
        var opacity = payload.Items["home.hero.overlay_opacity"];
        opacity.ResolvedLanguage.Should().Be("en");
        opacity.Number.Should().Be(0.35m);
    }

    [Fact]
    public async Task Media_reference_resolves_to_an_asset()
    {
        await SeedAsync();

        var payload = await GetSectionAsync("home", "en");

        var poster = payload.Items["home.hero.poster"];
        poster.Kind.Should().Be(ContentKind.ImageRef);
        poster.Media.Should().NotBeNull();
        poster.Media!.BlobUri.Should().StartWith("/media/");
    }

    [Fact]
    public async Task Unpublished_blocks_are_not_returned()
    {
        await SeedAsync();

        await using (var dbContext = _fixture.CreateContext())
        {
            var draft = new ContentBlock(
                ContentBlockId.New(), "home.unpublished.draft", ContentKind.PlainText,
                "home", "Draft", 999, isPublished: false, Now, "test");
            draft.SetTranslation("en", "Hidden", null, null, null, Now, "test");
            dbContext.ContentBlocks.Add(draft);
            await dbContext.SaveChangesAsync();
        }

        _revision.Bump(); // invalidate any cached payload so the read store is hit fresh

        var payload = await GetSectionAsync("home", "en");

        payload.Items.Should().NotContainKey("home.unpublished.draft");
        payload.Items.Should().ContainKey("home.hero.headline");
    }

    [Fact]
    public async Task Amenities_section_has_the_full_list()
    {
        await SeedAsync();

        var payload = await GetSectionAsync("amenities", "en");

        payload.Items.Should().HaveCount(51);
        payload.Items.Values.Should().OnlyContain(v => v.Kind == ContentKind.Boolean);
        payload.Items.Values.Count(v => v.Boolean == true).Should().Be(47);
        payload.Items.Values.Count(v => v.Boolean == false).Should().Be(4);
        payload.Items["amenities.not_available.smoke_alarm"].Boolean.Should().BeFalse();
        payload.Items["amenities.kitchen.dishwasher"].Boolean.Should().BeTrue();
    }

    [Fact]
    public async Task Property_capacity_is_seeded()
    {
        await SeedAsync();

        var property = await GetPropertyAsync();

        property.Should().NotBeNull();
        property!.Sleeps.Should().Be(6);
        property.Bedrooms.Should().Be(2);
        property.Beds.Should().Be(4);
        property.Bathrooms.Should().Be(2);
        property.BedsBreakdown.Should().Contain("sofa bed");
    }

    [Fact]
    public async Task Snapshot_line_is_derived_from_property_capacity()
    {
        await SeedAsync();

        var payload = await GetSectionAsync("home", "en");

        payload.Items["home.snapshot.line"].Text
            .Should().Be("2 bedrooms · 4 beds · 2 baths · Sleeps 6 · Vila Nova de Gaia");
    }

    [Fact]
    public async Task Reviews_are_seeded_with_real_verbatim_bodies()
    {
        await SeedAsync();

        var reviews = await GetReviewsAsync();

        reviews.Should().HaveCount(6);
        reviews.Should().OnlyContain(r => r.Source == "Airbnb" && r.Rating == 5);

        var aicha = reviews.Single(r => r.GuestFirstName == "Aicha");
        aicha.GuestLocation.Should().Be("Charlotte, North Carolina");
        aicha.Body.Should().Be(
            "Good location, very easy to get into the center of Porto by using bus 901 or 906. " +
            "Very convenient! The apartment was very modern with a beautiful view. The staff was " +
            "very nice and even checked on my son when I mentioned he was sick and recommended a " +
            "pharmacy near by that helped us out.");
    }
}
