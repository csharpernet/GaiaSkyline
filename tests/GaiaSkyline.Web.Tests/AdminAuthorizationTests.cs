using System.Net;
using FluentAssertions;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// Every /admin route (Stage 7 Tests list) rejects anonymous requests — the Owner policy (role + 2FA +
/// IP allowlist) front-doors them all via AdminControllerBase, so an unauthenticated GET must redirect
/// to the login page, never render content.
/// </summary>
public sealed class AdminAuthorizationTests(PublicSiteFactory factory) : IClassFixture<PublicSiteFactory>
{
    public static readonly TheoryData<string> AdminRoutes = new(
        "/admin",
        "/admin/content",
        "/admin/content/grid",
        "/admin/media",
        "/admin/media/gallery",
        "/admin/media/hero",
        "/admin/stories",
        "/admin/bookings",
        "/admin/bookings/new",
        "/admin/calendar",
        "/admin/calendar/duplicates",
        "/admin/prices",
        "/admin/prices/setup",
        "/admin/prices/export.csv",
        "/admin/payments",
        "/admin/reviews",
        "/admin/partners",
        "/admin/seo",
        "/admin/settings",
        "/admin/audit");

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task Anonymous_requests_are_redirected_to_the_owner_login(string route)
    {
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(new Uri(route, UriKind.Relative));

        // The Owner policy front-doors every admin route: anonymous requests bounce to a login page
        // (the cookie LoginPath — /en/account/login — whose page offers the owner sign-in), never 200.
        response.StatusCode.Should().Be(HttpStatusCode.Redirect, $"{route} must not serve anonymously");
        response.Headers.Location!.ToString().Should().Contain("login", $"{route} must bounce to a login page");
    }
}
