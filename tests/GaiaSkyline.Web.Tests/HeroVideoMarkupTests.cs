using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// With a live hero video seeded, the home page renders the full responsive markup: the six codec-qualified,
/// orientation-scoped <source>s, the poster <picture> (fetchpriority LCP), the decorative preload-none video
/// (no autoplay — the script drives it) and the hero-video script. This class gets its own factory/database,
/// so seeding a live hero does not affect the other site tests. Stage 7E-4c.
/// </summary>
public sealed class HeroVideoMarkupTests(PublicSiteFactory factory) : IClassFixture<PublicSiteFactory>
{
    private readonly PublicSiteFactory _factory = factory;

    [Fact]
    public async Task Home_renders_the_full_hero_video_markup_when_a_live_hero_exists()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await GaiaSkyline.Web.Testing.E2ESeeder.SeedHeroAsync(scope.ServiceProvider, CancellationToken.None);
        }

        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync(new Uri("/en", UriKind.Relative));

        html.Should().Contain("id=\"hero-video\"");
        html.Should().Contain("preload=\"none\"", "the hero video must not block rendering");
        html.Should().Contain("disablepictureinpicture");
        html.Should().NotContain("autoplay", "the script decides whether to play — no markup autoplay");

        // All three codecs, each orientation.
        html.Should().Contain("av01").And.Contain("vp9").And.Contain("avc1");
        html.Should().Contain("(orientation: portrait)").And.Contain("(orientation: landscape)");
        html.Should().Contain("-mobile-av1.webm").And.Contain("-desktop-h264.mp4");

        // The poster is the LCP element.
        html.Should().Contain("data-hero-poster");
        html.Should().Contain("fetchpriority=\"high\"");

        html.Should().Contain("hero-video.js");
    }
}
