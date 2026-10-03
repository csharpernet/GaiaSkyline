using System.Net;
using System.Net.Http.Headers;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GaiaSkyline.Web.Tests;

[Collection(PublicSiteCollection.Name)]
public class StripeWebhookApiTests(PublicSiteFactory factory)
{
    private HttpClient Client() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Webhook_rejects_a_request_with_an_invalid_signature()
    {
        using var client = Client();
        const string payload = """{"id":"evt_bad","object":"event","type":"payment_intent.succeeded","data":{"object":{}}}""";
        using var content = new StringContent(payload, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Headers.Add("Stripe-Signature", "t=1,v1=not-a-real-signature");

        using var response = await client.PostAsync(new Uri("/webhooks/stripe", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Webhook_rejects_a_request_with_no_signature_header()
    {
        using var client = Client();
        using var content = new StringContent("{}", Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var response = await client.PostAsync(new Uri("/webhooks/stripe", UriKind.Relative), content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
