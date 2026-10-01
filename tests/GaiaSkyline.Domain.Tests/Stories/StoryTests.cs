using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Stories;

namespace GaiaSkyline.Domain.Tests.Stories;

public class StoryTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Story CreateStory(string slug = "a-slow-morning") =>
        new(StoryId.New(), slug, MediaAssetId.New(), Now, isPublished: true, 1, "Home Me");

    [Fact]
    public void Valid_story_is_constructed_and_slug_is_lowercased()
    {
        var story = new Story(StoryId.New(), "  A-Slow-Morning  ", MediaAssetId.New(), Now, true, 1, "Home Me");

        story.Slug.Should().Be("a-slow-morning");
        story.AuthorName.Should().Be("Home Me");
        story.IsPublished.Should().BeTrue();
        story.Translations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("has spaces")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    [InlineData("double--hyphen")]
    [InlineData("under_score")]
    public void Rejects_invalid_slug(string slug)
    {
        var act = () => CreateStory(slug);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetTranslation_upserts_per_language()
    {
        var story = CreateStory();

        var first = story.SetTranslation("en", "Title", "Excerpt", "<p>Body</p>", null, null, 3);
        var second = story.SetTranslation("EN", "New title", "Excerpt", "<p>Body</p>", null, null, 4);

        story.Translations.Should().ContainSingle();
        second.Should().BeSameAs(first);
        story.Translations.Single().Title.Should().Be("New title");
        story.Translations.Single().ReadingTimeMinutes.Should().Be(4);
    }

    [Fact]
    public void SetTranslation_keeps_separate_rows_per_language()
    {
        var story = CreateStory();

        story.SetTranslation("en", "Title", "Excerpt", "<p>Body</p>", null, null, 3);
        story.SetTranslation("de", "Titel", "Auszug", "<p>Text</p>", null, null, 3);

        story.Translations.Should().HaveCount(2);
    }
}
