using FluentAssertions;
using GaiaSkyline.Application.Seo;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Web.Tests;

/// <summary>An owner meta override replaces the page's default &lt;title&gt;/description. Stage 7 §5.</summary>
public sealed class PageMetaOverrideTests(PublicSiteFactory factory) : IClassFixture<PublicSiteFactory>
{
    private readonly PublicSiteFactory _factory = factory;

    [Fact]
    public async Task An_override_replaces_the_page_title()
    {
        var title = "Douro Views " + Guid.NewGuid().ToString("N")[..6];
        using (var scope = _factory.Services.CreateScope())
        {
            var admin = scope.ServiceProvider.GetRequiredService<IPageMetaAdminService>();
            (await admin.UpsertAsync("gallery", "en", title, "An override description for the gallery.", noIndex: false, noFollow: false, "test", CancellationToken.None))
                .Should().BeTrue();
        }

        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync(new Uri("/en/gallery", UriKind.Relative));

        html.Should().Contain(title, "the override title replaces the page default");
        html.Should().Contain("An override description for the gallery.");
    }

    [Fact]
    public async Task A_noindex_override_emits_robots_and_drops_the_canonical()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var admin = scope.ServiceProvider.GetRequiredService<IPageMetaAdminService>();
            (await admin.UpsertAsync("book", "en", null, null, noIndex: true, noFollow: true, "test", CancellationToken.None))
                .Should().BeTrue();
        }

        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync(new Uri("/en/book", UriKind.Relative));

        html.Should().Contain("name=\"robots\" content=\"noindex, nofollow\"");
        html.Should().NotContain("rel=\"canonical\" href=\"http://localhost/en/book\"", "a noindex page drops its canonical");

        // Clean up so the shared factory DB does not leak noindex into other tests.
        using var scope2 = _factory.Services.CreateScope();
        var admin2 = scope2.ServiceProvider.GetRequiredService<IPageMetaAdminService>();
        await admin2.UpsertAsync("book", "en", null, null, noIndex: false, noFollow: false, "test", CancellationToken.None);
    }
}
