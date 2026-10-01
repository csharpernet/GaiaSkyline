using FluentAssertions;
using GaiaSkyline.Web.Localization;
using Microsoft.AspNetCore.Routing;

namespace GaiaSkyline.Web.Tests;

public class CultureRouteConstraintTests
{
    private static bool Matches(string? slug)
    {
        var constraint = new CultureRouteConstraint();
        var values = new RouteValueDictionary { ["lang"] = slug };
        return constraint.Match(httpContext: null, route: null, "lang", values, RouteDirection.IncomingRequest);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("pt-pt")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("EN")] // case-insensitive
    public void Accepts_enabled_language_slugs(string slug) => Matches(slug).Should().BeTrue();

    [Theory]
    [InlineData("xx")]
    [InlineData("pt")] // pt-pt is the slug, not pt
    [InlineData("en-us")]
    [InlineData("")]
    [InlineData("sitemap.xml")]
    public void Rejects_unknown_slugs(string slug) => Matches(slug).Should().BeFalse();

    [Fact]
    public void Rejects_missing_value() => Matches(null).Should().BeFalse();
}
