using System.Globalization;
using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Web.Media;

namespace GaiaSkyline.Web.Tests;

public class ResponsiveImageTests
{
    private static MediaAssetDto Asset(string blobUri, string? lqip = "data:image/webp;base64,AAAA") =>
        new(
            Id: Guid.NewGuid(),
            Kind: MediaKind.Image,
            BlobUri: blobUri,
            PosterBlobUri: null,
            Width: 1600,
            Height: 1066,
            DurationSec: null,
            ByteSize: 1234,
            ContentType: "image/jpeg",
            Alt: "A balcony view",
            Lqip: lqip);

    [Fact]
    public void Builds_webp_and_jpeg_srcsets_at_every_width_for_a_pipeline_asset()
    {
        var image = ResponsiveImage.From(
            Asset("/media/home-gallery-1-1600.jpg"),
            sizes: "100vw",
            wrapperClass: "aspect-[3/2]");

        image.Src.Should().Be("/media/home-gallery-1-1600.jpg");
        image.AvifSrcset.Should().Be(
            "/media/home-gallery-1-400.avif 400w, /media/home-gallery-1-800.avif 800w, /media/home-gallery-1-1600.avif 1600w");
        image.WebpSrcset.Should().Be(
            "/media/home-gallery-1-400.webp 400w, /media/home-gallery-1-800.webp 800w, /media/home-gallery-1-1600.webp 1600w");
        image.JpegSrcset.Should().Be(
            "/media/home-gallery-1-400.jpg 400w, /media/home-gallery-1-800.jpg 800w, /media/home-gallery-1-1600.jpg 1600w");
        image.Alt.Should().Be("A balcony view");
        image.Lqip.Should().StartWith("data:image/webp;base64,");
        image.Width.Should().Be(1600);
        image.Height.Should().Be(1066);
    }

    [Fact]
    public void Omits_srcsets_when_the_asset_is_not_a_pipeline_raster()
    {
        var image = ResponsiveImage.From(
            Asset("/media/legacy-photo.png"),
            sizes: "100vw",
            wrapperClass: "aspect-[3/2]");

        image.AvifSrcset.Should().BeNull();
        image.WebpSrcset.Should().BeNull();
        image.JpegSrcset.Should().BeNull();
        image.Src.Should().Be("/media/legacy-photo.png");
    }

    [Fact]
    public void Carries_eager_and_high_priority_hints_when_requested()
    {
        var hero = ResponsiveImage.From(
            Asset("/media/home-hero-poster-1600.jpg"),
            sizes: "100vw",
            wrapperClass: "aspect-[3/2]",
            eager: true,
            highPriority: true);

        hero.Eager.Should().BeTrue();
        hero.HighPriority.Should().BeTrue();
    }

    [Fact]
    public void Defaults_to_lazy_and_normal_priority()
    {
        var image = ResponsiveImage.From(
            Asset("/media/home-gallery-2-1600.jpg"),
            sizes: "100vw",
            wrapperClass: "aspect-[3/2]");

        image.Eager.Should().BeFalse();
        image.HighPriority.Should().BeFalse();
    }

    [Fact]
    public void Resolves_alt_for_the_request_culture_then_english_then_legacy()
    {
        var asset = Asset("/media/home-gallery-1-1600.jpg") with
        {
            AltByLang = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["en"] = "A balcony view",
                ["pt-PT"] = "Vista da varanda",
            },
        };

        WithCulture("pt-PT", () => ResponsiveImage.From(asset, "100vw", "aspect-[3/2]").Alt)
            .Should().Be("Vista da varanda");

        // No French row → English fallback.
        WithCulture("fr", () => ResponsiveImage.From(asset, "100vw", "aspect-[3/2]").Alt)
            .Should().Be("A balcony view");

        // No per-language rows at all → the legacy single value.
        WithCulture("fr", () => ResponsiveImage.From(Asset("/media/x-1600.jpg"), "100vw", "aspect-[3/2]").Alt)
            .Should().Be("A balcony view");
    }

    private static T WithCulture<T>(string culture, Func<T> act)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);
        try
        {
            return act();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
