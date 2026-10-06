using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Application.Storage;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>
/// AdminMediaService drives the media file store at the right moments (Stage 8 Part B, ADR 0024): an upload
/// publishes the new stem, a replace republishes it, and a rename moves old→new. In production this store is
/// Blob; here a recording fake proves the call contract without needing Azure (the Blob impl's end-to-end
/// behaviour is a deployment-phase verification).
/// </summary>
public sealed class MediaFileStorePublishTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Upload_then_rename_publishes_then_moves_the_stem()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gaia-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new RecordingFileStore();
        try
        {
            byte[] png;
            using (var src = new ImageMagick.MagickImage(ImageMagick.MagickColors.SteelBlue, 800, 600))
            {
                src.Format = ImageMagick.MagickFormat.Png;
                png = src.ToByteArray();
            }

            var suffix = Guid.NewGuid().ToString("N")[..8];
            Guid id;
            await using (var ctx = _fixture.CreateContext())
            {
                var service = new AdminMediaService(ctx, new ImageRenditionService(), store, new ContentRevision(), TimeProvider.System);
                id = await service.UploadImageAsync(new MemoryStream(png), dir, "alt", "Original " + suffix + ".png", "owner", CancellationToken.None);
            }

            store.Published.Should().ContainSingle();
            var publishedStem = store.Published[0].Stem;
            store.Published[0].Directory.Should().Be(dir);

            await using (var ctx = _fixture.CreateContext())
            {
                var service = new AdminMediaService(ctx, new ImageRenditionService(), store, new ContentRevision(), TimeProvider.System);
                (await service.RenameImageAsync(id, "Renamed " + suffix, dir, "owner", CancellationToken.None))
                    .Should().Be(MediaRenameResult.Renamed);
            }

            store.Moved.Should().ContainSingle();
            store.Moved[0].OldStem.Should().Be(publishedStem, "rename moves from the published stem");
            store.Moved[0].NewStem.Should().NotBe(publishedStem);

            await using var verify = _fixture.CreateContext();
            var asset = await verify.MediaAssets.FirstAsync(a => a.Id == MediaAssetId.From(id));
            asset.BlobUri.Should().Contain(store.Moved[0].NewStem, "the stored URL follows the new stem");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private sealed class RecordingFileStore : IMediaFileStore
    {
        public List<(string Directory, string Stem)> Published { get; } = [];

        public List<(string OldStem, string NewStem)> Moved { get; } = [];

        public bool ServesMedia => false;

        public Task PublishStemAsync(string workingDirectory, string stem, CancellationToken cancellationToken)
        {
            Published.Add((workingDirectory, stem));
            return Task.CompletedTask;
        }

        public Task MoveStemAsync(string workingDirectory, string oldStem, string newStem, CancellationToken cancellationToken)
        {
            Moved.Add((oldStem, newStem));
            return Task.CompletedTask;
        }

        public Task<MediaFileContent?> OpenAsync(string fileName, CancellationToken cancellationToken) =>
            Task.FromResult<MediaFileContent?>(null);
    }
}
