using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Content;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class AdminContentServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Translate_then_publish_makes_the_block_visible_and_bumps_the_revision()
    {
        var key = "test.block." + Guid.NewGuid().ToString("N")[..8];
        await using (var seed = _fixture.CreateContext())
        {
            seed.ContentBlocks.Add(new ContentBlock(
                ContentBlockId.New(), key, ContentKind.ShortText, "test", "Test block",
                displayOrder: 0, isPublished: false, DateTime.UtcNow, "seed"));
            await seed.SaveChangesAsync();
        }

        var revision = new ContentRevision();
        var before = revision.Current;

        await using var context = _fixture.CreateContext();
        var service = new AdminContentService(context, revision, new HtmlContentSanitizer(), TimeProvider.System);

        // Save draft: the published value stays null and the revision is not bumped (public unaffected).
        (await service.SetTranslationAsync(key, "en", new ContentValueDto("Hello published", null, null, null), "owner", CancellationToken.None))
            .Should().BeTrue();
        revision.Current.Should().Be(before);

        await using (var mid = _fixture.CreateContext())
        {
            var staged = (await mid.ContentBlocks.Include(b => b.Translations).FirstAsync(b => b.Key == key))
                .Translations.First(t => t.LanguageCode == "en");
            staged.ValueText.Should().BeNull();
            staged.HasDraft.Should().BeTrue();
            staged.DraftText.Should().Be("Hello published");
        }

        // Publish: promotes the draft to the published value and bumps the revision.
        (await service.PublishAsync(key, "owner", CancellationToken.None)).Should().BeTrue();
        revision.Current.Should().BeGreaterThan(before);

        await using var verify = _fixture.CreateContext();
        var saved = await verify.ContentBlocks.Include(b => b.Translations).FirstAsync(b => b.Key == key);
        saved.IsPublished.Should().BeTrue();
        var published = saved.Translations.First(t => t.LanguageCode == "en");
        published.ValueText.Should().Be("Hello published");
        published.HasDraft.Should().BeFalse();
    }

    [Fact]
    public async Task Batch_save_stages_only_changed_languages_then_publish_promotes_them()
    {
        var key = "test.block." + Guid.NewGuid().ToString("N")[..8];
        await using (var seed = _fixture.CreateContext())
        {
            var block = new ContentBlock(
                ContentBlockId.New(), key, ContentKind.ShortText, "test", "Test block",
                displayOrder: 0, isPublished: true, DateTime.UtcNow, "seed");
            block.SetTranslation("en", "English", null, null, null, DateTime.UtcNow, "seed");
            block.SetTranslation("fr", "[FR] English", null, null, null, DateTime.UtcNow, "seed");
            seed.ContentBlocks.Add(block);
            await seed.SaveChangesAsync();
        }

        var revision = new ContentRevision();
        await using var context = _fixture.CreateContext();
        var service = new AdminContentService(context, revision, new HtmlContentSanitizer(), TimeProvider.System);

        // Submit all languages but only change FR; EN is unchanged and must not be staged.
        var edits = new[]
        {
            new ContentTranslationEdit("en", new ContentValueDto("English", null, null, null)),
            new ContentTranslationEdit("fr", new ContentValueDto("Bonjour", null, null, null)),
        };
        (await service.SetTranslationsAsync(key, edits, "owner", CancellationToken.None)).Should().BeTrue();

        await using (var mid = _fixture.CreateContext())
        {
            var block = await mid.ContentBlocks.Include(b => b.Translations).FirstAsync(b => b.Key == key);
            block.Translations.First(t => t.LanguageCode == "en").HasDraft.Should().BeFalse("EN was unchanged");
            var fr = block.Translations.First(t => t.LanguageCode == "fr");
            fr.HasDraft.Should().BeTrue();
            fr.DraftText.Should().Be("Bonjour");
            fr.ValueText.Should().Be("[FR] English", "the public value is untouched until publish");
        }

        (await service.PublishAsync(key, "owner", CancellationToken.None)).Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        var published = (await verify.ContentBlocks.Include(b => b.Translations).FirstAsync(b => b.Key == key))
            .Translations.First(t => t.LanguageCode == "fr");
        published.ValueText.Should().Be("Bonjour");
        published.HasDraft.Should().BeFalse();
    }

    [Fact]
    public async Task Writing_an_unknown_key_returns_false()
    {
        await using var context = _fixture.CreateContext();
        var service = new AdminContentService(context, new ContentRevision(), new HtmlContentSanitizer(), TimeProvider.System);

        (await service.SetPublishedAsync("does.not.exist", true, "owner", CancellationToken.None)).Should().BeFalse();
    }
}
