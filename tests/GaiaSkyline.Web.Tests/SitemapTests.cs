using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Seo;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Stories_appear_in_every_language_each_under_its_own_slug()
    {
        // The seeded stories are EN-only, so every language publishes them under the canonical slug;
        // give one story a French slug and the FR URL must switch to it (Stage 7 §4).
        string canonical;
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            canonical = (await context.Stories.AsNoTracking().FirstAsync(s => s.IsPublished)).Slug;
        }

        using var client = _factory.CreateClient();
        var xml = await client.GetStringAsync(new Uri("/sitemap.xml", UriKind.Relative));

        xml.Should().Contain($"<loc>http://localhost/en/stories/{canonical}</loc>");
        xml.Should().Contain($"<loc>http://localhost/fr/stories/{canonical}</loc>", "languages without their own slug use the canonical one");
        xml.Should().Contain($"<loc>http://localhost/de/stories/{canonical}</loc>");

        // Give the story a French slug through the real write path, then the FR URL uses it.
        var frSlug = $"histoire-fr-{Guid.NewGuid():N}"[..20];
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var story = await context.Stories
                .Include(s => s.Translations).Include(s => s.Aliases)
                .FirstAsync(s => s.IsPublished && s.Slug == canonical);
            story.SetTranslation("fr", "Histoire (FR)", "Extrait", "<p>Corps</p>", null, null, 1);
            story.SetTranslationSlug("fr", frSlug, preservePreviousSlug: false);
            await context.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IContentRevision>().Bump();
        }

        try
        {
            var after = await client.GetStringAsync(new Uri("/sitemap.xml", UriKind.Relative));
            after.Should().Contain($"<loc>http://localhost/fr/stories/{frSlug}</loc>", "FR now has its own slug");
            after.Should().Contain($"<loc>http://localhost/en/stories/{canonical}</loc>", "EN keeps the canonical slug");
            after.Should().NotContain($"<loc>http://localhost/fr/stories/{canonical}</loc>");
            after.Should().Contain($"hreflang=\"fr\" href=\"http://localhost/fr/stories/{frSlug}\"",
                "the story's hreflang alternates follow the per-language slugs");
        }
        finally
        {
            // The factory's database is shared by every test in this class — undo the FR translation.
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var story = await context.Stories
                .Include(s => s.Translations)
                .FirstAsync(s => s.IsPublished && s.Slug == canonical);
            var fr = story.Translations.First(t => t.LanguageCode == "fr");
            context.Remove(fr);
            await context.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<IContentRevision>().Bump();
        }
    }
}
