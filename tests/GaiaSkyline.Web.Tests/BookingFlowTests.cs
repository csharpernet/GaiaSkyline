using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace GaiaSkyline.Web.Tests;

[Collection(PublicSiteCollection.Name)]
public class BookingFlowTests(PublicSiteFactory factory)
{
    // A valid far-future 5-night stay (seeded rate; satisfies the 3-night minimum).
    private const string CheckoutQuery = "checkIn=2027-07-01&checkOut=2027-07-06&adults=2&children=0&infants=0";

    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Checkout_page_is_noindex_and_loads_stripe()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri($"/en/book/checkout?{CheckoutQuery}", UriKind.Relative));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("name=\"robots\" content=\"noindex");
        html.Should().Contain("https://js.stripe.com/v3/");
        html.Should().Contain("id=\"payment-element\"");
    }

    [Fact]
    public async Task Checkout_page_csp_allows_stripe_and_payment()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri($"/en/book/checkout?{CheckoutQuery}", UriKind.Relative));

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.Should().Contain("https://js.stripe.com");
        csp.Should().Contain("https://api.stripe.com");
        csp.Should().Contain("frame-src https://js.stripe.com https://hooks.stripe.com");

        var permissions = response.Headers.GetValues("Permissions-Policy").Single();
        permissions.Should().Contain("payment=(self \"https://js.stripe.com\")");
    }

    [Fact]
    public async Task Home_csp_does_not_allow_stripe()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/en", UriKind.Relative));

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        csp.Should().NotContain("js.stripe.com");
        csp.Should().Contain("frame-src 'none'");
    }

    [Fact]
    public async Task Confirmation_requires_a_valid_token()
    {
        using var client = Client();

        using var noToken = await client.GetAsync(new Uri("/en/book/confirmation/GS-NOPE", UriKind.Relative));
        noToken.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var badToken = await client.GetAsync(new Uri("/en/book/confirmation/GS-NOPE?token=forged", UriKind.Relative));
        badToken.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Status_api_requires_a_valid_token()
    {
        using var client = Client();
        using var response = await client.GetAsync(new Uri("/api/book/status/GS-NOPE?token=forged", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Status_api_does_not_500_when_stripe_is_not_configured()
    {
        // Reproduces CI, where no Stripe key is set: the booking controller must still construct and
        // serve the status poll. An actual payment is the first thing that needs a real key, since the
        // Stripe client is resolved lazily. Without that, resolving the controller threw and returned 500.
        using var unconfigured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Stripe:SecretKey"] = string.Empty,
                    ["Stripe:PublishableKey"] = string.Empty,
                })));
        using var client = unconfigured.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(
            new Uri("/api/book/status/GS-NOPE?token=forged", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Home_booking_widget_links_into_book()
    {
        using var client = Client();
        var html = await client.GetStringAsync(new Uri("/en", UriKind.Relative));

        html.Should().Contain("id=\"booking-widget\"");
        html.Should().Contain("/js/booking-widget.js");
    }
}
