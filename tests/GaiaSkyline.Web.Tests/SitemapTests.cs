using FluentAssertions;
using GaiaSkyline.Application.Seo;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Web.Tests;

/// <summary>/sitemap.xml honors the owner's per-page, per-language noindex overrides. Stage 7 §5.</summary>
public sealed class SitemapTests(PublicSiteFactory factory) : IClassFixture<PublicSiteFactory>
{
    private readonly PublicSiteFactory _factory = factory;

    [Fact]
    public async Task Lists_every_language_of_a_page_by_default()
    {
        using var client = _factory.CreateClient();
        var xml = await client.GetStringAsync(new Uri("/sitemap.xml", UriKind.Relative));

        xml.Should().Contain("<loc>http://localhost/en/book</loc>");
        xml.Should().Contain("<loc>http://localhost/fr/book</loc>");
    }

    [Fact]
    public async Task A_noindex_override_drops_only_that_language()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var admin = scope.ServiceProvider.GetRequiredService<IPageMetaAdminService>();
            (await admin.UpsertAsync("gallery", "en", null, null, noIndex: true, noFollow: false, "test", CancellationToken.None))
                .Should().BeTrue();
        }

        using var client = _factory.CreateClient();
        var xml = await client.GetStringAsync(new Uri("/sitemap.xml", UriKind.Relative));

        xml.Should().NotContain("<loc>http://localhost/en/gallery</loc>", "the en gallery is noindex");
        xml.Should().Contain("<loc>http://localhost/fr/gallery</loc>", "the fr gallery is still indexable");
        // The dropped language must not survive as an hreflang alternate of the remaining ones either.
        xml.Should().NotContain("hreflang=\"en\" href=\"http://localhost/en/gallery\"");
    }
}
