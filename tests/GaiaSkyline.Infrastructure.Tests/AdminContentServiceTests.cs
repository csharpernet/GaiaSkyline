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
        var service = new AdminContentService(context, revision, TimeProvider.System);

        (await service.SetTranslationAsync(key, "en", new ContentValueDto("Hello published", null, null, null), "owner", CancellationToken.None))
            .Should().BeTrue();
        (await service.SetPublishedAsync(key, true, "owner", CancellationToken.None))
            .Should().BeTrue();

        revision.Current.Should().BeGreaterThan(before);

        await using var verify = _fixture.CreateContext();
        var saved = await verify.ContentBlocks.Include(b => b.Translations).FirstAsync(b => b.Key == key);
        saved.IsPublished.Should().BeTrue();
        saved.Translations.First(t => t.LanguageCode == "en").ValueText.Should().Be("Hello published");
    }

    [Fact]
    public async Task Writing_an_unknown_key_returns_false()
    {
        await using var context = _fixture.CreateContext();
        var service = new AdminContentService(context, new ContentRevision(), TimeProvider.System);

        (await service.SetPublishedAsync("does.not.exist", true, "owner", CancellationToken.None)).Should().BeFalse();
    }
}
