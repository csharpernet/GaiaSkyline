using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GaiaSkyline.Web.Tests;

[Collection(PublicSiteCollection.Name)]
public class BookPageTests(PublicSiteFactory factory)
{
    private static readonly string[] Slugs = ["en", "pt-pt", "es", "fr", "de"];

    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Theory]
    [InlineData("en", "en")]
    [InlineData("pt-pt", "pt-PT")]
    [InlineData("es", "es")]
    [InlineData("fr", "fr")]
    [InlineData("de", "de")]
    public async Task Book_page_renders_with_seo_in_every_language(string slug, string culture)
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri($"/{slug}/book", UriKind.Relative));

        Regex.Match(html, "<html lang=\"([^\"]+)\">").Groups[1].Value.Should().Be(culture);
        Regex.Match(html, "<link rel=\"canonical\" href=\"([^\"]+)\"").Groups[1].Value
            .Should().Be($"http://localhost/{slug}/book");
        Regex.Count(html, "<h1").Should().Be(1);
        html.Should().Contain("id=\"booking-form\"");
    }

    [Fact]
    public async Task Book_page_loads_flatpickr_and_the_booking_script_but_not_stripe()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en/book", UriKind.Relative));

        html.Should().Contain("/js/vendor/flatpickr.min.js");
        html.Should().Contain("/js/book.js");
        // Stripe.js must NOT load on the public /book page (keeps SEO/CWV budgets).
        html.Should().NotContain("js.stripe.com");
    }

    [Fact]
    public async Task Book_page_is_indexable()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/en/book", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        html.Should().NotContain("noindex");
    }
}
