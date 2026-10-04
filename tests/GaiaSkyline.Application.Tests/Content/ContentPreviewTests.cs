using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Application.Tests.Content;

public sealed class ContentPreviewTests
{
    private sealed class PreviewState(bool on) : IContentPreviewState
    {
        public bool IsPreview => on;
    }

    private static ContentBlock Block(string key, bool published) =>
        new(ContentBlockId.New(), key, ContentKind.ShortText, "home", key, 0, published, DateTime.UtcNow, "seed");

    private static ContentService Build(FakeContentReadStore store, bool preview) =>
        new(store, new MemoryCache(new MemoryCacheOptions()), new ContentRevision(), new PreviewState(preview));

    [Fact]
    public async Task Public_read_shows_the_published_value_preview_shows_the_draft()
    {
        var store = new FakeContentReadStore();
        var block = Block("home.headline", published: true);
        block.SetTranslation("en", "Old headline", null, null, null, DateTime.UtcNow, "seed");
        block.SetDraftTranslation("en", "New headline", null, null, null, DateTime.UtcNow, "owner");
        store.Blocks.Add(block);

        var published = await Build(store, preview: false).GetSectionAsync("home", "en", CancellationToken.None);
        published.Items["home.headline"].Text.Should().Be("Old headline");

        var preview = await Build(store, preview: true).GetSectionAsync("home", "en", CancellationToken.None);
        preview.Items["home.headline"].Text.Should().Be("New headline");
    }

    [Fact]
    public async Task Unpublished_block_is_hidden_publicly_but_visible_in_preview()
    {
        var store = new FakeContentReadStore();
        var block = Block("home.draftonly", published: false);
        block.SetTranslation("en", "Draft only", null, null, null, DateTime.UtcNow, "seed");
        store.Blocks.Add(block);

        var published = await Build(store, preview: false).GetSectionAsync("home", "en", CancellationToken.None);
        published.Items.Should().NotContainKey("home.draftonly");

        var preview = await Build(store, preview: true).GetSectionAsync("home", "en", CancellationToken.None);
        preview.Items["home.draftonly"].Text.Should().Be("Draft only");
    }
}
