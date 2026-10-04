using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;

namespace GaiaSkyline.Application.Tests.Content;

public sealed class ContentPlaceholdersTests
{
    [Theory]
    [InlineData("[PT] Olá")]
    [InlineData("[ES] Hola")]
    [InlineData("[FR] Bonjour")]
    [InlineData("[DE] Hallo")]
    [InlineData("  [FR] leading whitespace")]
    [InlineData("[de] lowercase prefix")]
    public void Flags_seeded_placeholder_prefixes(string text)
    {
        ContentPlaceholders.IsPlaceholder(text).Should().BeTrue();
    }

    [Theory]
    [InlineData("Bonjour")]
    [InlineData("A real [PT] mid-string is fine")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Does_not_flag_real_or_empty_values(string? text)
    {
        ContentPlaceholders.IsPlaceholder(text).Should().BeFalse();
    }

    [Theory]
    [InlineData(ContentKind.PlainText, true)]
    [InlineData(ContentKind.RichText, true)]
    [InlineData(ContentKind.ShortText, true)]
    [InlineData(ContentKind.Url, false)]
    [InlineData(ContentKind.Number, false)]
    [InlineData(ContentKind.Boolean, false)]
    [InlineData(ContentKind.ImageRef, false)]
    [InlineData(ContentKind.VideoRef, false)]
    public void Localized_text_kinds_are_the_translated_ones(ContentKind kind, bool expected)
    {
        kind.IsLocalizedText().Should().Be(expected);
    }
}
