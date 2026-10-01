using FluentAssertions;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Tests.Content;

public class ContentBlockTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static ContentBlock CreateBlock(
        string key = "home.hero.headline",
        ContentKind kind = ContentKind.PlainText) =>
        new(ContentBlockId.New(), key, kind, "home", "Hero headline", 1, isPublished: true, Now, "seed");

    [Fact]
    public void Valid_block_is_constructed_and_trimmed()
    {
        var block = new ContentBlock(
            ContentBlockId.New(), "  home.hero.headline  ", ContentKind.PlainText,
            " home ", " Hero headline ", 3, isPublished: true, Now, "seed");

        block.Key.Should().Be("home.hero.headline");
        block.Section.Should().Be("home");
        block.DisplayName.Should().Be("Hero headline");
        block.DisplayOrder.Should().Be(3);
        block.IsPublished.Should().BeTrue();
        block.Translations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Rejects_blank_key(string? key)
    {
        var act = () => new ContentBlock(
            ContentBlockId.New(), key!, ContentKind.PlainText, "home", "x", 1, true, Now, "seed");

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Rejects_blank_section(string? section)
    {
        var act = () => new ContentBlock(
            ContentBlockId.New(), "home.x", ContentKind.PlainText, section!, "x", 1, true, Now, "seed");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetTranslation_adds_a_new_translation()
    {
        var block = CreateBlock();

        block.SetTranslation("en", "Hello", null, null, null, Now, "seed");

        block.Translations.Should().ContainSingle();
        var translation = block.Translations.Single();
        translation.LanguageCode.Should().Be("en");
        translation.ValueText.Should().Be("Hello");
        translation.ContentBlockId.Should().Be(block.Id);
    }

    [Fact]
    public void SetTranslation_upserts_in_place_for_the_same_language()
    {
        var block = CreateBlock();

        var first = block.SetTranslation("en", "Hello", null, null, null, Now, "seed");
        var second = block.SetTranslation("en", "Updated", null, null, null, Now, "admin");

        block.Translations.Should().ContainSingle();
        second.Should().BeSameAs(first);
        block.Translations.Single().ValueText.Should().Be("Updated");
        block.Translations.Single().UpdatedBy.Should().Be("admin");
    }

    [Fact]
    public void SetTranslation_matches_language_case_insensitively()
    {
        var block = CreateBlock();

        block.SetTranslation("en", "Hello", null, null, null, Now, "seed");
        block.SetTranslation("EN", "Updated", null, null, null, Now, "admin");

        block.Translations.Should().ContainSingle();
    }

    [Fact]
    public void SetTranslation_keeps_separate_rows_per_language()
    {
        var block = CreateBlock();

        block.SetTranslation("en", "Hello", null, null, null, Now, "seed");
        block.SetTranslation("de", "Hallo", null, null, null, Now, "seed");

        block.Translations.Should().HaveCount(2);
    }

    [Fact]
    public void SetPublished_flips_the_draft_publish_switch()
    {
        var block = CreateBlock();

        block.SetPublished(false, Now, "admin");

        block.IsPublished.Should().BeFalse();
        block.UpdatedBy.Should().Be("admin");
    }
}
