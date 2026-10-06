using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Content;
using GaiaSkyline.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class AdminMediaServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    private static readonly string[] AllLanguages = ["en", "pt-PT", "es", "fr", "de"];

    private static MediaAsset NewImage() => new(
        MediaAssetId.New(), MediaKind.Image, $"/media/{Guid.NewGuid():N}-1600.jpg", null,
        1600, 1066, null, 123, "image/jpeg", DateTime.UtcNow, "seed");

    [Fact]
    public async Task Set_alt_texts_upserts_blanks_remove_and_bump_revision()
    {
        var asset = NewImage();
        await using (var seed = _fixture.CreateContext())
        {
            seed.MediaAssets.Add(asset);
            await seed.SaveChangesAsync();
        }

        var revision = new ContentRevision();
        var before = revision.Current;
        await using var context = _fixture.CreateContext();
        var service = new AdminMediaService(context, new ImageRenditionService(), new LocalDiskMediaFileStore(), revision, TimeProvider.System);

        var ok = await service.SetAltTextsAsync(
            asset.Id.Value,
            new Dictionary<string, string?> { ["en"] = "A balcony view", ["fr"] = "Vue du balcon", ["de"] = "  " },
            "owner", CancellationToken.None);
        ok.Should().BeTrue();
        revision.Current.Should().BeGreaterThan(before);

        await using var verify = _fixture.CreateContext();
        var saved = await verify.MediaAssets.Include(a => a.AltTexts).FirstAsync(a => a.Id == asset.Id);
        saved.AltTexts.Select(t => t.LanguageCode).Should().BeEquivalentTo(["en", "fr"]); // blank de not stored
        saved.AltTextFor("fr").Should().Be("Vue du balcon");
    }

    [Fact]
    public async Task Library_reports_readiness_usage_and_where_used()
    {
        var asset = NewImage();
        var blockKey = "mediatest." + Guid.NewGuid().ToString("N")[..8];
        await using (var seed = _fixture.CreateContext())
        {
            foreach (var l in AllLanguages)
            {
                asset.SetAltText(l, $"alt-{l}");
            }

            seed.MediaAssets.Add(asset);

            var block = new ContentBlock(
                ContentBlockId.New(), blockKey, ContentKind.ImageRef, "mediatest", "An image",
                0, isPublished: true, DateTime.UtcNow, "seed");
            block.SetTranslation("en", null, asset.Id, null, null, DateTime.UtcNow, "seed");
            seed.ContentBlocks.Add(block);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var read = new AdminMediaReadService(context);

        var library = await read.GetLibraryAsync(MediaKind.Image, includeDeleted: false, CancellationToken.None);
        var row = library.Single(m => m.Id == asset.Id.Value);
        row.ReadyForPublic.Should().BeTrue("all five languages have alt text");
        row.LanguagesWithAlt.Should().HaveCount(5);
        row.UsageCount.Should().BeGreaterThanOrEqualTo(1);

        var detail = await read.GetAssetAsync(asset.Id.Value, CancellationToken.None);
        detail!.ReadyForPublic.Should().BeTrue();
        detail.UsedBy.Should().Contain(u => u.Reference == blockKey);

        (await read.IsReadyForPublicAsync(asset.Id.Value, CancellationToken.None)).Should().BeTrue();
    }

    [Fact]
    public async Task Replace_regenerates_renditions_keeping_id_and_url()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gaia-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Seed an asset whose on-disk renditions exist (stem derived from the blob URL).
            var stem = "replace-" + Guid.NewGuid().ToString("N")[..8];
            var renditions = new ImageRenditionService();
            byte[] original;
            using (var src = new ImageMagick.MagickImage(ImageMagick.MagickColors.SteelBlue, 1000, 600))
            {
                src.Format = ImageMagick.MagickFormat.Png;
                original = src.ToByteArray();
            }

            await renditions.GenerateAsync(new MemoryStream(original), dir, stem, CancellationToken.None);
            var masterPath = Path.Combine(dir, $"{stem}-1600.jpg");
            var bytesBeforeReplace = await File.ReadAllBytesAsync(masterPath);

            var asset = new MediaAsset(
                MediaAssetId.New(), MediaKind.Image, $"/media/{stem}-1600.jpg", null,
                1600, 960, null, 10, "image/jpeg", DateTime.UtcNow, "seed");
            await using (var seed = _fixture.CreateContext())
            {
                seed.MediaAssets.Add(asset);
                await seed.SaveChangesAsync();
            }

            await using var context = _fixture.CreateContext();
            var service = new AdminMediaService(context, renditions, new LocalDiskMediaFileStore(), new ContentRevision(), TimeProvider.System);

            // Replace with a portrait image → the stored height changes but id and URL do not.
            byte[] replacement;
            using (var src = new ImageMagick.MagickImage(ImageMagick.MagickColors.Firebrick, 600, 1200))
            {
                src.Format = ImageMagick.MagickFormat.Png;
                replacement = src.ToByteArray();
            }

            (await service.ReplaceImageAsync(asset.Id.Value, new MemoryStream(replacement), dir, "owner", CancellationToken.None))
                .Should().BeTrue();

            await using var verify = _fixture.CreateContext();
            var saved = await verify.MediaAssets.FirstAsync(a => a.Id == asset.Id);
            saved.BlobUri.Should().Be($"/media/{stem}-1600.jpg", "the URL (and id) must survive so references keep resolving");
            saved.Height.Should().BeGreaterThan(saved.Width, "the portrait replacement changed the dimensions");
            File.Exists(Path.Combine(dir, $"{stem}-1600.avif")).Should().BeTrue();

            // Stage 8 Part B cache-busting: the SEO filename is unchanged but Version bumps, so the public
            // URL gains ?v=2 while the same stem now serves the replacement bytes.
            saved.Version.Should().Be(2, "a replace bumps the cache-busting version");
            var bytesAfterReplace = await File.ReadAllBytesAsync(masterPath);
            bytesAfterReplace.Should().NotEqual(bytesBeforeReplace, "the same URL now serves the new bytes");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Soft_delete_is_blocked_while_in_use_then_succeeds_and_hides_from_the_library()
    {
        var asset = NewImage();
        var blockKey = "mediatest." + Guid.NewGuid().ToString("N")[..8];
        await using (var seed = _fixture.CreateContext())
        {
            seed.MediaAssets.Add(asset);
            var block = new ContentBlock(
                ContentBlockId.New(), blockKey, ContentKind.ImageRef, "mediatest", "An image",
                0, isPublished: true, DateTime.UtcNow, "seed");
            block.SetTranslation("en", null, asset.Id, null, null, DateTime.UtcNow, "seed");
            seed.ContentBlocks.Add(block);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var service = new AdminMediaService(context, new ImageRenditionService(), new LocalDiskMediaFileStore(), new ContentRevision(), TimeProvider.System);
        var read = new AdminMediaReadService(context);

        (await service.SoftDeleteAsync(asset.Id.Value, "owner", CancellationToken.None))
            .Should().Be(MediaDeleteResult.InUse);

        // Remove the reference, then it can be deleted and drops out of the default library.
        await using (var detach = _fixture.CreateContext())
        {
            var block = await detach.ContentBlocks.Include(b => b.Translations).FirstAsync(b => b.Key == blockKey);
            detach.ContentBlocks.Remove(block);
            await detach.SaveChangesAsync();
        }

        (await service.SoftDeleteAsync(asset.Id.Value, "owner", CancellationToken.None))
            .Should().Be(MediaDeleteResult.Deleted);

        (await read.GetLibraryAsync(MediaKind.Image, includeDeleted: false, CancellationToken.None))
            .Should().NotContain(m => m.Id == asset.Id.Value);
        (await read.GetLibraryAsync(MediaKind.Image, includeDeleted: true, CancellationToken.None))
            .Should().Contain(m => m.Id == asset.Id.Value);

        (await service.RestoreAsync(asset.Id.Value, "owner", CancellationToken.None)).Should().BeTrue();
        (await read.GetLibraryAsync(MediaKind.Image, includeDeleted: false, CancellationToken.None))
            .Should().Contain(m => m.Id == asset.Id.Value);
    }

    [Fact]
    public async Task Gallery_read_lists_items_in_order_and_available_images_then_save_reorders_and_sets_hero()
    {
        var key = "testgallery." + Guid.NewGuid().ToString("N")[..8];
        var a = NewImage();
        var b = NewImage();
        var c = NewImage(); // available (not added to the collection)
        await using (var seed = _fixture.CreateContext())
        {
            seed.MediaAssets.AddRange(a, b, c);
            var collection = new MediaCollection(MediaCollectionId.New(), key, "Test gallery");
            collection.AddItem(MediaCollectionItemId.New(), a.Id, 0, isHero: true);
            collection.AddItem(MediaCollectionItemId.New(), b.Id, 1, isHero: false);
            seed.MediaCollections.Add(collection);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var read = new AdminMediaReadService(context);
        var service = new AdminMediaService(context, new ImageRenditionService(), new LocalDiskMediaFileStore(), new ContentRevision(), TimeProvider.System);

        var model = await read.GetCollectionAsync(key, CancellationToken.None);
        model.Should().NotBeNull();
        model!.Items.Select(i => i.MediaAssetId).Should().Equal(a.Id.Value, b.Id.Value);
        model.Items.Single(i => i.MediaAssetId == a.Id.Value).IsHero.Should().BeTrue();
        model.Available.Should().Contain(i => i.MediaAssetId == c.Id.Value);

        // Reorder (b, a) and make b the hero.
        var ok = await service.SetCollectionItemsAsync(key, new[]
        {
            new CollectionItemDto(b.Id.Value, IsHero: true),
            new CollectionItemDto(a.Id.Value, IsHero: false),
        }, "owner", CancellationToken.None);
        ok.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        var after = await new AdminMediaReadService(verify).GetCollectionAsync(key, CancellationToken.None);
        after!.Items.Select(i => i.MediaAssetId).Should().Equal(b.Id.Value, a.Id.Value);
        after.Items.Single(i => i.MediaAssetId == b.Id.Value).IsHero.Should().BeTrue();
    }

    private static byte[] Png(int w = 1000, int h = 600)
    {
        using var src = new ImageMagick.MagickImage(ImageMagick.MagickColors.SteelBlue, (uint)w, (uint)h)
        {
            Format = ImageMagick.MagickFormat.Png,
        };
        return src.ToByteArray();
    }

    [Fact]
    public async Task Upload_derives_the_filename_from_the_title_and_dedupes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gaia-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Unique base so a duplicate within this test (not leftovers from another) drives the "-2" suffix.
            var title = "Douro Balcony " + Guid.NewGuid().ToString("N")[..8];
            var expected = MediaSlug.From(title);
            var png = Png();

            await using var context = _fixture.CreateContext();
            var service = new AdminMediaService(context, new ImageRenditionService(), new LocalDiskMediaFileStore(), new ContentRevision(), TimeProvider.System);

            var id1 = await service.UploadImageAsync(new MemoryStream(png), dir, "alt", title + ".png", "owner", CancellationToken.None);
            var id2 = await service.UploadImageAsync(new MemoryStream(png), dir, "alt", title + ".png", "owner", CancellationToken.None);

            await using var verify = _fixture.CreateContext();
            var m1 = await verify.MediaAssets.FirstAsync(a => a.Id == MediaAssetId.From(id1));
            var m2 = await verify.MediaAssets.FirstAsync(a => a.Id == MediaAssetId.From(id2));

            m1.BlobUri.Should().Be($"/media/{expected}-1600.jpg", "the filename comes from the title, not the GUID");
            m2.BlobUri.Should().Be($"/media/{expected}-2-1600.jpg", "a duplicate title gets a numeric suffix");
            File.Exists(Path.Combine(dir, $"{expected}-800.webp")).Should().BeTrue();
            File.Exists(Path.Combine(dir, $"{expected}-400.avif")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Rename_moves_the_renditions_updates_the_url_and_keeps_the_old_url_redirecting()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gaia-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var oldStem = MediaSlug.From("Original Name " + suffix);
            var newStem = MediaSlug.From("Renamed Image " + suffix);
            var png = Png();

            Guid id;
            await using (var upload = _fixture.CreateContext())
            {
                var service = new AdminMediaService(upload, new ImageRenditionService(), new LocalDiskMediaFileStore(), new ContentRevision(), TimeProvider.System);
                id = await service.UploadImageAsync(new MemoryStream(png), dir, "alt", "Original Name " + suffix + ".png", "owner", CancellationToken.None);
            }

            File.Exists(Path.Combine(dir, $"{oldStem}-1600.jpg")).Should().BeTrue();

            await using (var rename = _fixture.CreateContext())
            {
                var service = new AdminMediaService(rename, new ImageRenditionService(), new LocalDiskMediaFileStore(), new ContentRevision(), TimeProvider.System);
                (await service.RenameImageAsync(id, "Renamed Image " + suffix, dir, "owner", CancellationToken.None))
                    .Should().Be(MediaRenameResult.Renamed);
            }

            // The whole raster set moved to the new stem.
            File.Exists(Path.Combine(dir, $"{oldStem}-1600.jpg")).Should().BeFalse();
            File.Exists(Path.Combine(dir, $"{newStem}-1600.jpg")).Should().BeTrue();
            File.Exists(Path.Combine(dir, $"{newStem}-400.avif")).Should().BeTrue();

            await using var verify = _fixture.CreateContext();
            var saved = await verify.MediaAssets.Include(a => a.Aliases).FirstAsync(a => a.Id == MediaAssetId.From(id));
            saved.BlobUri.Should().Be($"/media/{newStem}-1600.jpg", "the URL follows the new filename");
            saved.Aliases.Select(a => a.OldSlug).Should().Contain(oldStem, "the old URL must keep working");

            // The resolver (used by the 301 middleware) maps the old stem to the current one.
            var resolver = new MediaAliasResolver(verify);
            (await resolver.ResolveCurrentStemAsync(oldStem, CancellationToken.None)).Should().Be(newStem);
            (await resolver.ResolveCurrentStemAsync("no-such-stem-" + suffix, CancellationToken.None)).Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Image_without_alt_in_all_languages_is_not_ready()
    {
        var asset = NewImage();
        await using (var seed = _fixture.CreateContext())
        {
            asset.SetAltText("en", "English only");
            seed.MediaAssets.Add(asset);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var read = new AdminMediaReadService(context);

        (await read.IsReadyForPublicAsync(asset.Id.Value, CancellationToken.None)).Should().BeFalse();
        var detail = await read.GetAssetAsync(asset.Id.Value, CancellationToken.None);
        detail!.MissingAltLanguages.Should().BeEquivalentTo(["pt-PT", "es", "fr", "de"]);
    }
}
