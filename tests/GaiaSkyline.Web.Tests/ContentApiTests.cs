using System.Net;
using System.Text.Json;
using FluentAssertions;
using GaiaSkyline.Application.Content;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GaiaSkyline.Web.Tests;

public sealed class ContentApiTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public ContentApiTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(webHost =>
        {
            webHost.UseEnvironment("Development");
            webHost.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Features:SeedContentOnStartup"] = "false",
                    // Fake-store API tests must not spin up Hangfire against a real database.
                    ["BackgroundJobs:Enabled"] = "false",
                }));
            webHost.ConfigureTestServices(services =>
            {
                services.RemoveAll<IContentReadStore>();
                services.AddSingleton<IContentReadStore, FakeContentReadStore>();
            });
        });
    }

    public void Dispose() => _factory.Dispose();

    private HttpClient CreateClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static JsonElement Items(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("items").Clone();
    }

    [Fact]
    public async Task Home_in_english_returns_authoritative_text()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri("/api/content/home?lang=en", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = Items(await response.Content.ReadAsStringAsync());
        var headline = items.GetProperty("home.hero.headline");
        headline.GetProperty("text").GetString().Should().Be("Wake up to the Dom Luís I Bridge");
        headline.GetProperty("resolvedLanguage").GetString().Should().Be("en");
        headline.GetProperty("kind").GetString().Should().Be("PlainText");
    }

    [Fact]
    public async Task Home_in_german_returns_placeholders_and_falls_back_for_numbers()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri("/api/content/home?lang=de", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = Items(await response.Content.ReadAsStringAsync());

        var headline = items.GetProperty("home.hero.headline");
        headline.GetProperty("resolvedLanguage").GetString().Should().Be("de");
        headline.GetProperty("text").GetString().Should().Be("[DE] Wake up to the Dom Luís I Bridge");

        var opacity = items.GetProperty("home.hero.overlay_opacity");
        opacity.GetProperty("resolvedLanguage").GetString().Should().Be("en"); // fell back
        opacity.GetProperty("number").GetDecimal().Should().Be(0.35m);
    }

    [Fact]
    public async Task Default_language_is_english_when_no_lang_is_given()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri("/api/content/home", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = Items(await response.Content.ReadAsStringAsync());
        items.GetProperty("home.hero.headline").GetProperty("resolvedLanguage").GetString().Should().Be("en");
    }

    [Fact]
    public async Task Reviews_endpoint_returns_published_reviews()
    {
        using var client = CreateClient();

        using var response = await client.GetAsync(new Uri("/api/reviews", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain(FakeContentReadStore.KnownReviewBody);
        json.Should().Contain("Aicha");
    }

    [Fact]
    public async Task Media_endpoint_returns_asset_or_404()
    {
        using var client = CreateClient();

        using var found = await client.GetAsync(
            new Uri($"/api/media/{FakeContentReadStore.KnownMediaId.Value}", UriKind.Relative));
        found.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await found.Content.ReadAsStringAsync();
        json.Should().Contain("/media/known.svg");

        using var missing = await client.GetAsync(
            new Uri($"/api/media/{Guid.NewGuid()}", UriKind.Relative));
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
