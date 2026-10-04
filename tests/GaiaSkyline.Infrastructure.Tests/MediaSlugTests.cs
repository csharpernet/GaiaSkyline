using FluentAssertions;
using GaiaSkyline.Infrastructure.Media;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class MediaSlugTests
{
    [Theory]
    [InlineData("Douro Balcony", "douro-balcony")]
    [InlineData("Douro Balcony.JPG", "douro-balcony")]
    [InlineData("Ponte Luís I à noite", "ponte-luis-i-a-noite")]
    [InlineData("  Spaced   Out  ", "spaced-out")]
    [InlineData("Weird__chars!!@#name", "weird-chars-name")]
    [InlineData("---leading-and-trailing---", "leading-and-trailing")]
    [InlineData("IMG_1234", "img-1234")]
    [InlineData("Café Münchën", "cafe-munchen")]
    public void Produces_a_clean_lowercase_ascii_slug(string title, string expected)
    {
        MediaSlug.From(title).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("...")]
    public void Returns_empty_when_nothing_usable_remains(string? title)
    {
        MediaSlug.From(title).Should().BeEmpty();
    }

    [Fact]
    public void Caps_the_length_without_a_trailing_hyphen()
    {
        var slug = MediaSlug.From(new string('a', 200));

        slug.Length.Should().BeLessThanOrEqualTo(120);
        slug.Should().NotEndWith("-");
    }
}
