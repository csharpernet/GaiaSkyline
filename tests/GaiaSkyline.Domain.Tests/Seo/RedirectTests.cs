using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Seo;

namespace GaiaSkyline.Domain.Tests.Seo;

public class RedirectTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Redirect Create(string from, string to, bool permanent = true) =>
        new(RedirectId.New(), from, to, permanent, Now, "owner");

    [Fact]
    public void Normalises_from_path_to_lowercase_no_trailing_slash()
    {
        var r = Create("/EN/Old-Page/", "/en/New-Page");

        r.FromPath.Should().Be("/en/old-page");
        r.ToPath.Should().Be("/en/New-Page", "the destination keeps its case");
    }

    [Theory]
    [InlineData("relative/path", "/to")]
    [InlineData("/from", "no-slash")]
    [InlineData("", "/to")]
    [InlineData("   ", "/to")]
    public void Rejects_non_rooted_paths(string from, string to)
    {
        var act = () => Create(from, to);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Rejects_a_self_redirect()
    {
        var act = () => Create("/en/page", "/EN/Page/"); // normalise to the same from-path
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("/en/a", "/en/a")]
    [InlineData("/en/a/", "/en/a")]
    public void NormalizeFrom_is_idempotent_and_slash_insensitive(string input, string expected)
    {
        Redirect.NormalizeFrom(input).Should().Be(expected);
    }
}
