using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Web.Media;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// The media cache-busting token (Stage 8 Part B): a never-replaced asset (Version 1) emits clean URLs so
/// existing cached links stay valid, while a replaced asset (Version 2+) carries <c>?v=</c> on the fallback
/// source and every srcset entry — the SEO filename is unchanged, only the token differs.
/// </summary>
public sealed class ResponsiveImageVersionTests
{
    private static MediaAssetDto Asset(int version) => new(
        Guid.NewGuid(), MediaKind.Image, "/media/balcony-view-1600.jpg", null,
        1600, 1066, null, 1234, "image/jpeg", "A balcony view", null, version);

    [Fact]
    public void Version_one_emits_no_token_so_cached_urls_stay_valid()
    {
        var image = ResponsiveImage.From(Asset(version: 1), sizes: "100vw", wrapperClass: "aspect-[3/2]");

        image.Src.Should().Be("/media/balcony-view-1600.jpg");
        image.AvifSrcset.Should().Be(
            "/media/balcony-view-400.avif 400w, /media/balcony-view-800.avif 800w, /media/balcony-view-1600.avif 1600w");
        image.WebpSrcset.Should().NotContain("?v=");
        image.JpegSrcset.Should().NotContain("?v=");
    }

    [Fact]
    public void A_replaced_asset_carries_the_token_on_the_source_and_every_srcset_entry()
    {
        var image = ResponsiveImage.From(Asset(version: 2), sizes: "100vw", wrapperClass: "aspect-[3/2]");

        image.Src.Should().Be("/media/balcony-view-1600.jpg?v=2");
        image.AvifSrcset.Should().Be(
            "/media/balcony-view-400.avif?v=2 400w, /media/balcony-view-800.avif?v=2 800w, /media/balcony-view-1600.avif?v=2 1600w");
        image.WebpSrcset.Should().Be(
            "/media/balcony-view-400.webp?v=2 400w, /media/balcony-view-800.webp?v=2 800w, /media/balcony-view-1600.webp?v=2 1600w");
        image.JpegSrcset.Should().Be(
            "/media/balcony-view-400.jpg?v=2 400w, /media/balcony-view-800.jpg?v=2 800w, /media/balcony-view-1600.jpg?v=2 1600w");
    }

    [Fact]
    public void The_dto_exposes_a_consistent_versioned_url_for_og_and_direct_links()
    {
        Asset(version: 1).VersionedBlobUri.Should().Be("/media/balcony-view-1600.jpg");
        Asset(version: 3).VersionedBlobUri.Should().Be("/media/balcony-view-1600.jpg?v=3");
    }
}
