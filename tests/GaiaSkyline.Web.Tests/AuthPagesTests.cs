using System.Net;
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
    public async Task Robots_blocks_account_and_my()
    {
        using var client = Client();
        var robots = await client.GetStringAsync(new Uri("/robots.txt", UriKind.Relative));

        robots.Should().Contain("Disallow: /*/account/");
        robots.Should().Contain("Disallow: /*/my/");
    }
}
