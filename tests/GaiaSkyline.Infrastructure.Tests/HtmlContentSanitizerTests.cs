using FluentAssertions;
using GaiaSkyline.Infrastructure.Content;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class HtmlContentSanitizerTests
{
    private readonly HtmlContentSanitizer _sanitizer = new();

    [Fact]
    public void Keeps_allowed_formatting_and_hardens_links()
    {
        var result = _sanitizer.Sanitize(
            "<p>Hello <strong>world</strong> <em>x</em> <a href=\"https://example.test\">link</a></p><ul><li>one</li></ul>");

        result.Should().Contain("<strong>world</strong>");
        result.Should().Contain("<em>x</em>");
        result.Should().Contain("<li>one</li>");
        result.Should().Contain("href=\"https://example.test\"");
        result.Should().Contain("rel=\"noopener noreferrer\"");
    }

    [Fact]
    public void Strips_script_disallowed_tags_and_event_handlers()
    {
        var result = _sanitizer.Sanitize(
            "<p onclick=\"steal()\">hi</p><script>alert(1)</script><iframe src=\"https://evil.test\"></iframe>");

        result.Should().NotContain("script");
        result.Should().NotContain("iframe");
        result.Should().NotContain("onclick");
        result.Should().Contain("hi");
    }

    [Fact]
    public void Strips_javascript_scheme_links()
    {
        var result = _sanitizer.Sanitize("<a href=\"javascript:alert(1)\">x</a>");
        result.Should().NotContain("javascript:");
    }

    [Fact]
    public void Empty_or_null_input_is_empty()
    {
        _sanitizer.Sanitize(null).Should().BeEmpty();
        _sanitizer.Sanitize("   ").Should().BeEmpty();
    }
}
