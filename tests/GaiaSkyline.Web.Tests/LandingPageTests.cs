using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GaiaSkyline.Web.Tests;

public class LandingPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    private HttpClient CreateClient() =>
        _factory
            .WithWebHostBuilder(webHost =>
            {
                webHost.UseEnvironment("Development");
                // Keep the landing-page tests database-free: do not seed on startup.
                webHost.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["Features:SeedContentOnStartup"] = "false" }));
            })
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Landing_page_returns_200_and_renders_the_brand_in_the_markup()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Gaia Skyline");
        html.Should().Contain("font-display"); // proves the Tailwind token classes reach the view
    }

    [Fact]
    public async Task Liveness_probe_returns_200()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Security_headers_and_csp_nonce_are_emitted()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        response.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        response.Headers.GetValues("Referrer-Policy").Should().Contain("strict-origin-when-cross-origin");
        response.Headers.Should().ContainKey("Permissions-Policy");

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("'nonce-");
    }
}
