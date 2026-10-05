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

    [Fact]
    public void Rename_keeps_the_old_slug_as_an_alias_when_preserving()
    {
        var story = CreateStory("old-slug");

        story.Rename("new-slug", preservePreviousSlug: true);

        story.Slug.Should().Be("new-slug");
        story.Aliases.Select(a => a.OldSlug).Should().ContainSingle().Which.Should().Be("old-slug");
    }

    [Fact]
    public void Rename_without_preserving_records_no_alias()
    {
        var story = CreateStory("draft-slug");

        story.Rename("final-slug", preservePreviousSlug: false);

        story.Slug.Should().Be("final-slug");
        story.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void Renaming_back_to_a_previous_slug_drops_that_alias()
    {
        var story = CreateStory("a");
        story.Rename("b", preservePreviousSlug: true); // alias: a

        story.Rename("a", preservePreviousSlug: true); // reclaim a → alias: b only

        story.Slug.Should().Be("a");
        story.Aliases.Select(a => a.OldSlug).Should().BeEquivalentTo(["b"]);
    }

    [Fact]
    public void Rename_to_the_same_slug_is_a_no_op()
    {
        var story = CreateStory("same");

        story.Rename("same", preservePreviousSlug: true);

        story.Slug.Should().Be("same");
        story.Aliases.Should().BeEmpty();
    }

    [Fact]
    public void SetTranslation_allows_a_title_only_draft_with_empty_excerpt_and_body()
    {
        var story = CreateStory();

        var act = () => story.SetTranslation("en", "Just a title", string.Empty, string.Empty, null, null, 1);

        act.Should().NotThrow();
        var t = story.Translations.Single();
        t.Excerpt.Should().BeEmpty();
        t.BodyRichText.Should().BeEmpty();
    }

    [Fact]
    public void SetTranslation_still_requires_a_title()
    {
        var story = CreateStory();

        var act = () => story.SetTranslation("en", "  ", "excerpt", "<p>body</p>", null, null, 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Publish_and_unpublish_toggle_the_flag()
    {
        var story = CreateStory();
        story.Unpublish();
        story.IsPublished.Should().BeFalse();
        story.Publish();
        story.IsPublished.Should().BeTrue();
    }
}
