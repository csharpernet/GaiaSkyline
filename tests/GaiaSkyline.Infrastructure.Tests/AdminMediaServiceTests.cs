using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Content;
using GaiaSkyline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class AdminMediaServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    private static readonly string[] AllLanguages = ["en", "pt-PT", "es", "fr", "de"];

    private static MediaAsset NewImage() => new(
        MediaAssetId.New(), MediaKind.Image, $"/media/{Guid.NewGuid():N}-1600.jpg", null,
        1600, 1066, null, 123, "image/jpeg", DateTime.UtcNow, "seed");

    [Fact]
    public async Task Set_alt_texts_upserts_blanks_remove_and_bump_revision()
    {
        var asset = NewImage();
        await using (var seed = _fixture.CreateContext())
        {
            seed.MediaAssets.Add(asset);
            await seed.SaveChangesAsync();
        }

        var revision = new ContentRevision();
        var before = revision.Current;
        await using var context = _fixture.CreateContext();
        var service = new AdminMediaService(context, new ImageRenditionService(), revision, TimeProvider.System);

        var ok = await service.SetAltTextsAsync(
            asset.Id.Value,
            new Dictionary<string, string?> { ["en"] = "A balcony view", ["fr"] = "Vue du balcon", ["de"] = "  " },
            "owner", CancellationToken.None);
        ok.Should().BeTrue();
        revision.Current.Should().BeGreaterThan(before);

        await using var verify = _fixture.CreateContext();
        var saved = await verify.MediaAssets.Include(a => a.AltTexts).FirstAsync(a => a.Id == asset.Id);
        saved.AltTexts.Select(t => t.LanguageCode).Should().BeEquivalentTo(["en", "fr"]); // blank de not stored
        saved.AltTextFor("fr").Should().Be("Vue du balcon");
    }

    [Fact]
    public async Task Library_reports_readiness_usage_and_where_used()
    {
        var asset = NewImage();
        var blockKey = "mediatest." + Guid.NewGuid().ToString("N")[..8];
        await using (var seed = _fixture.CreateContext())
        {
            foreach (var l in AllLanguages)
            {
                asset.SetAltText(l, $"alt-{l}");
            }

            seed.MediaAssets.Add(asset);

            var block = new ContentBlock(
                ContentBlockId.New(), blockKey, ContentKind.ImageRef, "mediatest", "An image",
                0, isPublished: true, DateTime.UtcNow, "seed");
            block.SetTranslation("en", null, asset.Id, null, null, DateTime.UtcNow, "seed");
            seed.ContentBlocks.Add(block);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var read = new AdminMediaReadService(context);

        var library = await read.GetLibraryAsync(MediaKind.Image, CancellationToken.None);
        var row = library.Single(m => m.Id == asset.Id.Value);
        row.ReadyForPublic.Should().BeTrue("all five languages have alt text");
        row.LanguagesWithAlt.Should().HaveCount(5);
        row.UsageCount.Should().BeGreaterThanOrEqualTo(1);

        var detail = await read.GetAssetAsync(asset.Id.Value, CancellationToken.None);
        detail!.ReadyForPublic.Should().BeTrue();
        detail.UsedBy.Should().Contain(u => u.Reference == blockKey);

        (await read.IsReadyForPublicAsync(asset.Id.Value, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Image_without_alt_in_all_languages_is_not_ready()
    {
        var asset = NewImage();
        await using (var seed = _fixture.CreateContext())
        {
            asset.SetAltText("en", "English only");
            seed.MediaAssets.Add(asset);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var read = new AdminMediaReadService(context);

        (await read.IsReadyForPublicAsync(asset.Id.Value, CancellationToken.None)).Should().BeFalse();
        var detail = await read.GetAssetAsync(asset.Id.Value, CancellationToken.None);
        detail!.MissingAltLanguages.Should().BeEquivalentTo(["pt-PT", "es", "fr", "de"]);
    }
}
