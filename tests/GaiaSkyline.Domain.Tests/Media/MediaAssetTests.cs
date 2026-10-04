using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Domain.Tests.Media;

public class MediaAssetTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static MediaAsset CreateImage(
        string? blobUri = "/media/abc.jpg",
        string? contentType = "image/jpeg",
        int width = 1600,
        int height = 1066) =>
        new(MediaAssetId.New(), MediaKind.Image, blobUri!, null, width, height, null, 123456, contentType!, Now, "seed");

    [Fact]
    public void Valid_image_is_constructed()
    {
        var asset = CreateImage();

        asset.Kind.Should().Be(MediaKind.Image);
        asset.BlobUri.Should().Be("/media/abc.jpg");
        asset.Width.Should().Be(1600);
        asset.DurationSec.Should().BeNull();
    }

    [Fact]
    public void Video_can_carry_a_poster_and_duration()
    {
        var video = new MediaAsset(
            MediaAssetId.New(), MediaKind.Video, "/media/hero.mp4", "/media/hero-poster.jpg",
            1920, 1080, 42, 9_000_000, "video/mp4", Now, "seed");

        video.Kind.Should().Be(MediaKind.Video);
        video.PosterBlobUri.Should().Be("/media/hero-poster.jpg");
        video.DurationSec.Should().Be(42);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Rejects_blank_blob_uri(string? blobUri)
    {
        var act = () => CreateImage(blobUri: blobUri);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, -1)]
    public void Rejects_negative_dimensions(int width, int height)
    {
        var act = () => CreateImage(width: width, height: height);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Set_alt_text_upserts_and_blank_removes()
    {
        var asset = CreateImage();

        asset.SetAltText("en", "A balcony view");
        asset.SetAltText("pt-PT", "Vista da varanda");
        asset.AltTexts.Should().HaveCount(2);

        asset.SetAltText("pt-PT", "Vista do Douro"); // update
        asset.AltTexts.Single(a => a.LanguageCode == "pt-PT").Text.Should().Be("Vista do Douro");

        asset.SetAltText("pt-PT", "   "); // blank removes
        asset.AltTexts.Should().ContainSingle(a => a.LanguageCode == "en");
    }

    [Fact]
    public void Alt_text_for_resolves_language_then_english_then_legacy()
    {
        var asset = new MediaAsset(
            MediaAssetId.New(), MediaKind.Image, "/media/abc.jpg", null, 1600, 1066, null, 1, "image/jpeg",
            Now, "seed", altText: "Legacy EN");

        // No per-language rows yet: everything falls back to the legacy value.
        asset.AltTextFor("fr").Should().Be("Legacy EN");

        asset.SetAltText("en", "Explicit EN");
        asset.AltTextFor("fr").Should().Be("Explicit EN"); // falls back to explicit en over legacy

        asset.SetAltText("fr", "Vue du balcon");
        asset.AltTextFor("fr").Should().Be("Vue du balcon"); // exact language wins
    }

    [Fact]
    public void Has_explicit_alt_text_for_all_languages_ignores_fallback()
    {
        var langs = new[] { "en", "pt-PT", "es", "fr", "de" };
        var asset = new MediaAsset(
            MediaAssetId.New(), MediaKind.Image, "/media/abc.jpg", null, 1600, 1066, null, 1, "image/jpeg",
            Now, "seed", altText: "Legacy EN");

        // The legacy value is a fallback, not an explicit per-language row.
        asset.HasExplicitAltTextForAllLanguages(langs).Should().BeFalse();

        foreach (var l in langs)
        {
            asset.SetAltText(l, $"alt-{l}");
        }

        asset.HasExplicitAltTextForAllLanguages(langs).Should().BeTrue();
    }
}
