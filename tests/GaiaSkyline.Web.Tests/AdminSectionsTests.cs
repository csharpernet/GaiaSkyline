using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// Every /admin section is behind the Owner policy (role + 2FA + IP allowlist). Anonymous requests are
/// redirected to the login path. Authenticated rendering is covered by the Playwright admin suite.
/// </summary>
[Collection(PublicSiteCollection.Name)]
public class AdminSectionsTests(PublicSiteFactory factory)
{
    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/audit")]
    public async Task Admin_routes_require_sign_in(string path)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/account/login");
    }
}
