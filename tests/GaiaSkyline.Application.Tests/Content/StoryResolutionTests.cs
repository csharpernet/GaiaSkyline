using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Stories;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Application.Tests.Content;

public sealed class StoryResolutionTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly FakeContentReadStore _store = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ContentRevision _revision = new();

    public void Dispose() => _cache.Dispose();

    private ContentService Service() => new(_store, _cache, _revision);

    private Story AddStory(string slug, DateTime published)
    {
        var cover = MediaAssetId.New();
        _store.Media[cover] = new MediaAsset(
            cover, MediaKind.Image, "/media/cover.svg", null, 1600, 1066, null, 1, "image/svg+xml", Now, "seed", "A cover");
        var story = new Story(StoryId.New(), slug, cover, published, isPublished: true, 1, "Home Me");
        _store.Stories.Add(story);
        return story;
    }

    [Fact]
    public async Task Story_in_requested_language_is_returned()
    {
        var story = AddStory("my-story", Now);
        story.SetTranslation("en", "English", "e", "<p>b</p>", null, null, 2);
        story.SetTranslation("de", "Deutsch", "d", "<p>t</p>", null, null, 2);

        var result = await Service().GetStoryAsync("my-story", "de", CancellationToken.None);

        result!.Title.Should().Be("Deutsch");
        result.ResolvedLanguage.Should().Be("de");
    }

    [Fact]
    public async Task Story_falls_back_to_english_when_language_missing()
    {
        var story = AddStory("my-story", Now);
        story.SetTranslation("en", "English title", "Excerpt", "<p>Body</p>", null, null, 2);

        var result = await Service().GetStoryAsync("my-story", "de", CancellationToken.None);

        result.Should().NotBeNull();
        result!.Title.Should().Be("English title");
        result.ResolvedLanguage.Should().Be("en");
        result.Cover.Should().NotBeNull();
    }

    [Fact]
    public async Task Published_stories_are_newest_first_and_take_limits()
    {
        AddStory("older", Now.AddDays(-10)).SetTranslation("en", "Older", "e", "<p>b</p>", null, null, 1);
        AddStory("newer", Now).SetTranslation("en", "Newer", "e", "<p>b</p>", null, null, 1);

        var top = await Service().GetPublishedStoriesAsync("en", take: 1, CancellationToken.None);

        top.Should().ContainSingle();
        top[0].Slug.Should().Be("newer");
    }
}
