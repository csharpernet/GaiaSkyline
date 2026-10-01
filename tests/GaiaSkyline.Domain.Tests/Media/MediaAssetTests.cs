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
}
