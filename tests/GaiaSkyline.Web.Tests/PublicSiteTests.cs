using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GaiaSkyline.Web.Tests;

[Collection(PublicSiteCollection.Name)]
public class PublicSiteTests(PublicSiteFactory factory)
{
    private static readonly string[] Slugs = ["en", "pt-pt", "es", "fr", "de"];
    private static readonly string[] Paths =
    [
        "", "gallery", "stories", "book",
        "legal/terms", "legal/privacy", "legal/cancellation-policy", "legal/al-registration",
    ];

    private const string StorySlug = "a-slow-morning-the-hot-tub-sunrise-and-port-wine-at-home";

    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public static IEnumerable<object[]> AllPages =>
        from slug in Slugs
        from path in Paths.Concat([$"stories/{StorySlug}"])
        select new object[] { slug, path };

    [Theory]
    [MemberData(nameof(AllPages))]
    public async Task Every_page_returns_200_in_every_language(string slug, string path)
    {
        using var client = Client();
        var url = string.IsNullOrEmpty(path) ? $"/{slug}" : $"/{slug}/{path}";

        using var response = await client.GetAsync(new Uri(url, UriKind.Relative));

        var body = response.StatusCode == HttpStatusCode.OK
            ? string.Empty
            : await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"{url} should resolve. Server said: {body}");
    }

    [Theory]
    [InlineData("en", "en")]
    [InlineData("pt-pt", "pt-PT")]
    [InlineData("es", "es")]
    [InlineData("fr", "fr")]
    [InlineData("de", "de")]
    public async Task Home_has_html_lang_canonical_and_full_hreflang(string slug, string culture)
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri($"/{slug}", UriKind.Relative));

        Regex.Match(html, "<html lang=\"([^\"]+)\">").Groups[1].Value.Should().Be(culture);
        Regex.Match(html, "<link rel=\"canonical\" href=\"([^\"]+)\"").Groups[1].Value
            .Should().Be($"http://localhost/{slug}");

        var hreflangs = Regex.Matches(html, "rel=\"alternate\" hreflang=\"([^\"]+)\" href=\"([^\"]+)\"");
        var langs = hreflangs.Select(m => m.Groups[1].Value).ToList();
        langs.Should().Contain(["en", "pt-PT", "es", "fr", "de", "x-default"]);

        // Exactly one <h1>.
        Regex.Count(html, "<h1").Should().Be(1);
    }

    [Fact]
    public async Task Hreflang_graph_is_reciprocal()
    {
        using var client = Client();
        var en = await client.GetStringAsync(new Uri("/en", UriKind.Relative));
        var pt = await client.GetStringAsync(new Uri("/pt-pt", UriKind.Relative));

        var enAlternates = Alternates(en);
        var ptAlternates = Alternates(pt);

        enAlternates.Should().BeEquivalentTo(ptAlternates);
        enAlternates.Should().Contain("http://localhost/en");
        enAlternates.Should().Contain("http://localhost/pt-pt");
    }

    [Fact]
    public async Task Root_redirects_302_to_a_language()
    {
        using var client = Client();

        using var response = await client.GetAsync(new Uri("/", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect); // 302
        response.Headers.Location!.ToString().Should().MatchRegex("^/(en|pt-pt|es|fr|de)$");
    }

    [Fact]
    public async Task Unknown_language_returns_404()
    {
        using var client = Client();

        using var response = await client.GetAsync(new Uri("/xx", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Story_renders_in_every_language_with_english_fallback()
    {
        using var client = Client();

        foreach (var slug in Slugs)
        {
            using var response = await client.GetAsync(new Uri($"/{slug}/stories/{StorySlug}", UriKind.Relative));
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var html = await response.Content.ReadAsStringAsync();
            html.Should().Contain("A Slow Morning"); // English title (fallback) renders in all languages
        }
    }

    [Fact]
    public async Task Sitemap_is_wellformed_and_covers_every_page_and_language()
    {
        using var client = Client();

        using var response = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/xml");

        var xml = await response.Content.ReadAsStringAsync();
        var doc = XDocument.Parse(xml); // throws if malformed
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = doc.Descendants(ns + "loc").Select(e => e.Value).ToList();

        locs.Should().Contain("http://localhost/en");
        locs.Should().Contain("http://localhost/pt-pt/gallery");
        locs.Should().Contain("http://localhost/de/legal/al-registration");
        locs.Should().Contain($"http://localhost/fr/stories/{StorySlug}");
        // 8 static pages + 3 stories = 11 pages x 5 languages.
        locs.Should().HaveCount(11 * 5);
    }

    [Fact]
    public async Task Robots_txt_blocks_private_areas_and_references_sitemap()
    {
        using var client = Client();

        var robots = await client.GetStringAsync(new Uri("/robots.txt", UriKind.Relative));

        robots.Should().Contain("Disallow: /admin/");
        robots.Should().Contain("Disallow: /api/");
        robots.Should().Contain("Disallow: /partners/dashboard/");
        robots.Should().Contain("Disallow: /my/");
        robots.Should().Contain("Disallow: /webhooks/");
        robots.Should().Contain("Sitemap: http://localhost/sitemap.xml");
    }

    [Fact]
    public async Task Liveness_probe_and_security_headers()
    {
        using var client = Client();

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        live.StatusCode.Should().Be(HttpStatusCode.OK);

        using var home = await client.GetAsync(new Uri("/en", UriKind.Relative));
        home.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        home.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("default-src 'self'");
    }

    [Fact]
    public async Task Home_emits_valid_lodgingbusiness_jsonld()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en", UriKind.Relative));

        var lodging = JsonLdBlocks(html).Single(e => e.GetProperty("@type").GetString() == "LodgingBusiness");

        lodging.GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
        lodging.GetProperty("address").GetProperty("addressLocality").GetString().Should().Be("Vila Nova de Gaia");
        lodging.GetProperty("geo").GetProperty("latitude").GetDouble().Should().BeApproximately(41.137, 0.1);
        lodging.GetProperty("checkinTime").GetString().Should().Be("16:00");
        lodging.GetProperty("numberOfRooms").GetInt32().Should().Be(2);
        // The aggregate is computed from the published reviews (Stage 7 §10): six seeded 5-star reviews.
        lodging.GetProperty("aggregateRating").GetProperty("ratingValue").GetDecimal().Should().Be(5m);
        lodging.GetProperty("aggregateRating").GetProperty("reviewCount").GetDecimal().Should().Be(6m);
        lodging.GetProperty("review").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Stories_index_emits_collectionpage_jsonld()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en/stories", UriKind.Relative));

        var page = JsonLdBlocks(html).Single(e => e.GetProperty("@type").GetString() == "CollectionPage");

        page.GetProperty("mainEntity").GetProperty("itemListElement").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Story_detail_emits_article_jsonld()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri($"/en/stories/{StorySlug}", UriKind.Relative));

        var article = JsonLdBlocks(html).Single(e => e.GetProperty("@type").GetString() == "Article");

        article.GetProperty("headline").GetString().Should().Contain("Slow Morning");
        article.GetProperty("datePublished").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
        article.GetProperty("author").GetProperty("name").GetString().Should().NotBeNullOrWhiteSpace();
        article.GetProperty("inLanguage").GetString().Should().Be("en");
    }

    [Fact]
    public async Task Non_home_pages_emit_breadcrumblist_jsonld()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en/gallery", UriKind.Relative));

        var crumbs = JsonLdBlocks(html).Single(e => e.GetProperty("@type").GetString() == "BreadcrumbList");

        crumbs.GetProperty("itemListElement").GetArrayLength().Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Gallery_page_shows_images_with_lightbox_hooks()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en/gallery", UriKind.Relative));

        html.Should().Contain("id=\"gallery-grid\"");
        html.Should().Contain("id=\"lightbox\"");
        Regex.Count(html, "data-gallery-open").Should().BeGreaterThan(5);
    }

    private static List<string> Alternates(string html) =>
        Regex.Matches(html, "rel=\"alternate\" hreflang=\"(?!x-default)[^\"]+\" href=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToList();

    private static IEnumerable<JsonElement> JsonLdBlocks(string html) =>
        Regex.Matches(html, "<script type=\"application/ld\\+json\"[^>]*>(.*?)</script>", RegexOptions.Singleline)
            .Select(m => JsonDocument.Parse(m.Groups[1].Value).RootElement.Clone());
}
