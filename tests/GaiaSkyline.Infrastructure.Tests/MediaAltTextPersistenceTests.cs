using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Content;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class MediaAltTextPersistenceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Alt_texts_round_trip_and_the_read_store_includes_them()
    {
        var id = MediaAssetId.New();
        await using (var seed = _fixture.CreateContext())
        {
            var asset = new MediaAsset(
                id, MediaKind.Image, $"/media/{Guid.NewGuid():N}-1600.jpg", null,
                1600, 1066, null, 123, "image/jpeg", DateTime.UtcNow, "seed", altText: "Legacy EN");
            asset.SetAltText("en", "A balcony view");
            asset.SetAltText("pt-PT", "Vista da varanda");
            seed.MediaAssets.Add(asset);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var store = new ContentReadStore(context);
        var loaded = await store.GetMediaAssetAsync(id, CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.AltTexts.Should().HaveCount(2);
        loaded.AltTextFor("pt-PT").Should().Be("Vista da varanda");
        loaded.AltTextFor("fr").Should().Be("A balcony view"); // English fallback
    }
}
