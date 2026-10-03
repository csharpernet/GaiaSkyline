using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// The E2E test seam must be OFF unless explicitly enabled. The default test host does not set
/// <c>E2E:Enabled</c>, so the capture endpoint must 404 — proving the seam can't leak in normal runs
/// (or in Production, which also never sets the flag).
/// </summary>
[Collection(PublicSiteCollection.Name)]
public class E2ESeamTests(PublicSiteFactory factory)
{
    [Fact]
    public async Task Test_email_endpoint_is_not_available_when_the_seam_is_off()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(new Uri("/test/emails", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
