using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GaiaSkyline.Web.Tests;

[Collection(PublicSiteCollection.Name)]
public class BookingApiTests(PublicSiteFactory factory)
{
    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Quote_api_returns_a_server_computed_breakdown()
    {
        using var client = Client();
        var request = new { checkIn = "2027-07-01", checkOut = "2027-07-06", adults = 2, children = 0, infants = 0 };

        using var response = await client.PostAsJsonAsync(new Uri("/api/quote", UriKind.Relative), request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.GetProperty("nights").GetInt32().Should().Be(5);
        root.GetProperty("currency").GetString().Should().Be("EUR");
        // Seeded placeholder: 5 nights * 120 + 60 cleaning = 660 (no discount under 7 nights, no tax).
        root.GetProperty("cleaningFee").GetDecimal().Should().Be(60m);
        root.GetProperty("total").GetDecimal().Should().Be(660m);
    }

    [Fact]
    public async Task Quote_api_rejects_a_stay_below_the_minimum_nights()
    {
        using var client = Client();
        var request = new { checkIn = "2027-07-01", checkOut = "2027-07-02", adults = 2, children = 0, infants = 0 };

        using var response = await client.PostAsJsonAsync(new Uri("/api/quote", UriKind.Relative), request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Availability_api_returns_blocked_dates()
    {
        using var client = Client();
        var request = new { from = "2027-07-01", to = "2027-08-01" };

        using var response = await client.PostAsJsonAsync(new Uri("/api/availability", UriKind.Relative), request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.TryGetProperty("blockedDates", out var blocked).Should().BeTrue();
        blocked.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task Availability_api_rejects_an_inverted_range()
    {
        using var client = Client();
        var request = new { from = "2027-08-01", to = "2027-07-01" };

        using var response = await client.PostAsJsonAsync(new Uri("/api/availability", UriKind.Relative), request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
