using FluentAssertions;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Content;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class AdminContentReadServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Overview_flags_empty_and_placeholder_text_but_not_english_only_kinds()
    {
        var section = "readtest." + Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;

        var textKey = section + ".headline";
        var numberKey = section + ".count";
        await using (var seed = _fixture.CreateContext())
        {
            var text = new ContentBlock(ContentBlockId.New(), textKey, ContentKind.ShortText, section, "Headline",
                0, isPublished: true, now, "seed");
            text.SetTranslation("en", "Hello", null, null, null, now, "seed");
            text.SetTranslation("fr", "[FR] Hello", null, null, null, now, "seed"); // placeholder
            text.SetTranslation("es", "", null, null, null, now, "seed");           // empty
            // pt-PT and de: no row at all -> empty

            // Number is English-authoritative: a missing non-English value is a fallback, not a gap.
            var number = new ContentBlock(ContentBlockId.New(), numberKey, ContentKind.Number, section, "Count",
                1, isPublished: true, now, "seed");
            number.SetTranslation("en", null, null, 3m, null, now, "seed");

            seed.ContentBlocks.AddRange(text, number);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var read = new AdminContentReadService(context);
        var overview = await read.GetOverviewAsync(CancellationToken.None);

        var block = overview.Sections.Single(s => s.Key == section).Blocks.Single(b => b.Key == textKey);
        block.Languages.Single(l => l.LanguageCode == "en").IsEmpty.Should().BeFalse();
        block.Languages.Single(l => l.LanguageCode == "fr").IsPlaceholder.Should().BeTrue();
        block.Languages.Single(l => l.LanguageCode == "es").IsEmpty.Should().BeTrue();
        block.Languages.Single(l => l.LanguageCode == "pt-PT").IsEmpty.Should().BeTrue();
        block.Languages.Single(l => l.LanguageCode == "de").IsEmpty.Should().BeTrue();

        var numberBlock = overview.Sections.Single(s => s.Key == section).Blocks.Single(b => b.Key == numberKey);
        numberBlock.Languages.Should().OnlyContain(l => !l.IsEmpty, "Number is English-authoritative and falls back");
    }

    [Fact]
    public async Task GetBlock_returns_all_five_languages_with_draft_and_published_values()
    {
        var key = "readtest." + Guid.NewGuid().ToString("N")[..8] + ".body";
        var now = DateTime.UtcNow;
        await using (var seed = _fixture.CreateContext())
        {
            var block = new ContentBlock(ContentBlockId.New(), key, ContentKind.PlainText, "readtest", "Body",
                0, isPublished: true, now, "seed");
            block.SetTranslation("en", "Published EN", null, null, null, now, "seed");
            block.SetDraftTranslation("fr", "Draft FR", null, null, null, now, "seed");
            seed.ContentBlocks.Add(block);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var read = new AdminContentReadService(context);
        var detail = await read.GetBlockAsync(key, CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.Languages.Should().HaveCount(5);
        detail.Languages.Single(l => l.LanguageCode == "en").Text.Should().Be("Published EN");
        var fr = detail.Languages.Single(l => l.LanguageCode == "fr");
        fr.HasDraft.Should().BeTrue();
        fr.DraftText.Should().Be("Draft FR");
    }

    [Fact]
    public async Task GetAssets_filters_by_kind()
    {
        var now = DateTime.UtcNow;
        var imageId = MediaAssetId.New();
        await using (var seed = _fixture.CreateContext())
        {
            seed.MediaAssets.Add(new MediaAsset(imageId, MediaKind.Image, "/media/x.webp", null,
                800, 600, null, 1234, "image/webp", now, "seed", "Alt text"));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var read = new AdminContentReadService(context);

        var images = await read.GetAssetsAsync(MediaKind.Image, CancellationToken.None);
        images.Should().Contain(a => a.Id == imageId.Value);

        var videos = await read.GetAssetsAsync(MediaKind.Video, CancellationToken.None);
        videos.Should().NotContain(a => a.Id == imageId.Value);
    }
}
