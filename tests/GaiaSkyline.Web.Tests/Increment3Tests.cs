using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// Stage 3 / Increment 3: performance + CWV scaffolding (responsive images, LQIP, output-cache /
/// CSP reconciliation, web-vitals, favicons, lazy map).
/// </summary>
[Collection(PublicSiteCollection.Name)]
public class Increment3Tests(PublicSiteFactory factory)
{
    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Vitals_endpoint_accepts_a_beacon_and_returns_204()
    {
        using var client = Client();
        const string body = """{"name":"LCP","value":1234.5,"rating":"good","id":"v1-1","path":"/en"}""";
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/api/vitals", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Home_renders_responsive_picture_with_webp_jpeg_and_lqip()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en", UriKind.Relative));

        html.Should().Contain("<picture>");
        html.Should().Contain("type=\"image/webp\"");
        html.Should().Contain("type=\"image/jpeg\"");
        // srcset exposes every pipeline width.
        html.Should().MatchRegex(@"home-gallery-1-400\.webp 400w");
        html.Should().MatchRegex(@"home-gallery-1-1600\.webp 1600w");
        // LQIP is inlined as a data URI behind the image.
        html.Should().Contain("data:image/webp;base64,");
        // Explicit dimensions on the rendered image keep CLS at zero.
        html.Should().MatchRegex(@"<img[^>]+width=""1600""[^>]+height=""1066""");
    }

    [Fact]
    public async Task Home_preloads_the_hero_poster_for_lcp()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en", UriKind.Relative));

        html.Should().MatchRegex(
            @"<link rel=""preload"" as=""image"" href=""/media/home-hero-poster-1600\.jpg"" fetchpriority=""high"" />");
    }

    [Fact]
    public async Task Home_loads_self_hosted_web_vitals_reporting()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en", UriKind.Relative));

        html.Should().Contain("/js/vendor/web-vitals.iife.js");
        html.Should().Contain("/js/vitals.js");
    }

    [Fact]
    public async Task Home_references_all_favicon_variants()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en", UriKind.Relative));

        html.Should().Contain("href=\"/favicon.svg\"");
        html.Should().Contain("href=\"/favicon.ico\"");
        html.Should().Contain("href=\"/apple-touch-icon.png\"");
        html.Should().Contain("href=\"/favicon-192.png\"");
    }

    [Fact]
    public async Task Home_has_lazy_map_container_with_coarse_coordinates()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en", UriKind.Relative));

        html.Should().Contain("id=\"location-map\"");
        html.Should().Contain("data-js=\"/js/vendor/maplibre-gl.js\"");
        // Tiles are proxied through our own origin (Stage 7 §5 / ADR 0018), not fetched from OSM directly.
        html.Should().Contain("data-tiles=\"/map/tiles/{z}/{x}/{y}.png\"");
        html.Should().NotContain("tile.openstreetmap.org");

        // Coarse: coordinates are rounded to 3 decimals, not the precise stored value.
        var lat = Regex.Match(html, @"data-lat=""([^""]+)""").Groups[1].Value;
        lat.Should().Be("41.137");
        lat.Should().NotContain("41.1370");
    }

    [Fact]
    public async Task Csp_allows_map_tiles_and_blob_worker_but_body_carries_no_nonce()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/en", UriKind.Relative));
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        var html = await response.Content.ReadAsStringAsync();

        csp.Should().Contain("worker-src 'self' blob:");
        // Tiles are proxied same-origin now, so no external tile host appears in the CSP (ADR 0018).
        csp.Should().Contain("connect-src 'self'");
        csp.Should().Contain("img-src 'self' data: blob:");
        csp.Should().NotContain("tile.openstreetmap.org");
        csp.Should().MatchRegex(@"script-src 'self' 'nonce-[^']+'");

        // The cached body must not depend on the per-request nonce (else output caching would serve
        // a stale nonce against a fresh one). JSON-LD is data, not executable, so it carries none.
        html.Should().NotContain("nonce=");
        html.Should().Contain("application/ld+json");
    }
}
