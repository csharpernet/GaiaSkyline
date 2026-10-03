using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GaiaSkyline.Web.Tests;

[Collection(PublicSiteCollection.Name)]
public class AuthPagesTests(PublicSiteFactory factory)
{
    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Login_page_renders_and_is_noindex()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/en/account/login", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Sign in");
        html.Should().Contain("name=\"robots\" content=\"noindex");
    }

    [Fact]
    public async Task Register_page_offers_guest_and_partner()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en/account/register", UriKind.Relative));

        html.Should().Contain("value=\"Guest\"");
        html.Should().Contain("value=\"Partner\"");
    }

    [Fact]
    public async Task Admin_login_renders_in_english_and_is_noindex()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/admin/login", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Owner sign-in");
        html.Should().Contain("noindex");
    }

    [Fact]
    public async Task Admin_dashboard_redirects_anonymous_to_login()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/admin", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/account/login");
    }

    [Fact]
    public async Task Magic_link_request_page_renders()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/en/account/magic-link", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Access your booking");
        html.Should().Contain("Booking reference");
    }

    [Fact]
    public async Task My_bookings_redirects_anonymous_to_login()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/en/my/bookings", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/account/login");
    }

    [Fact]
    public async Task Partner_me_requires_a_bearer_token()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/api/partner/me", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Partner_refresh_rejects_a_bogus_token()
    {
        using var client = Client();
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/partner/token/refresh", UriKind.Relative), new { refreshToken = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_content_write_requires_auth()
    {
        using var client = Client();
        using var response = await client.PostAsync(
            new Uri("/api/admin/content/home.hero.headline/publish", UriKind.Relative), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Admin_media_collection_write_requires_auth()
    {
        using var client = Client();
        using var response = await client.PutAsJsonAsync(
            new Uri("/api/admin/media/collections/home.gallery/items", UriKind.Relative),
            Array.Empty<object>());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Robots_blocks_account_and_my()
    {
        using var client = Client();
        var robots = await client.GetStringAsync(new Uri("/robots.txt", UriKind.Relative));

        robots.Should().Contain("Disallow: /*/account/");
        robots.Should().Contain("Disallow: /*/my/");
    }
}
