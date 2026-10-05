using System.Net;
using FluentAssertions;
using GaiaSkyline.Application.Seo;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Web.Tests;

/// <summary>A configured redirect rule is applied by the before-routing middleware. Stage 7 §5.</summary>
public sealed class RedirectMiddlewareTests(PublicSiteFactory factory) : IClassFixture<PublicSiteFactory>
{
    private readonly PublicSiteFactory _factory = factory;

    [Fact]
    public async Task A_configured_redirect_301s_the_from_path_to_the_to_path()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IRedirectAdminService>();
            var result = await service.AddAsync("/en/old-promo", "/en/book", permanent: true, "test", CancellationToken.None);
            result.Ok.Should().BeTrue(result.Error);
        }

        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var response = await client.GetAsync(new Uri("/en/old-promo", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.MovedPermanently);
        response.Headers.Location!.ToString().Should().Be("/en/book");
    }
}
