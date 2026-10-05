using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Application.Seo;
using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Application.Tests;

/// <summary>The SEO warnings service flags images without full alt text and published stories with gaps. Stage 7 §5.</summary>
public sealed class SeoWarningsServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Flags_uncovered_images_missing_translations_and_missing_meta_descriptions()
    {
        var uncoveredImageId = Guid.NewGuid();
        var media = new FakeMediaRead(
        [
            new MediaLibraryItemDto(uncoveredImageId, MediaKind.Image, "/media/douro-sunset-1600.jpg", null, 1600, 900, 1000, Now, "owner",
                LanguagesWithAlt: ["en", "pt-PT"], ReadyForPublic: false, UsageCount: 1, IsDeleted: false),
            // Fully covered → no warning.
            new MediaLibraryItemDto(Guid.NewGuid(), MediaKind.Image, "/media/covered-1600.jpg", null, 1600, 900, 1000, Now, "owner",
                LanguagesWithAlt: ["en", "pt-PT", "es", "fr", "de"], ReadyForPublic: true, UsageCount: 0, IsDeleted: false),
        ]);

        var publishedId = Guid.NewGuid();
        var draftId = Guid.NewGuid();
        var list = new[]
        {
            new StoryListItemDto(publishedId, "douro-views", IsPublished: true, Now, "Douro Views", 3, 2, null),
            new StoryListItemDto(draftId, "a-draft", IsPublished: false, Now, "A Draft", 1, 4, null),
        };
        var edits = new Dictionary<Guid, StoryEditDto>
        {
            [publishedId] = new StoryEditDto(publishedId, "douro-views", true, Now, Guid.NewGuid(), null, "Ana",
                Translations:
                [
                    new StoryTranslationEditDto("en", "Douro Views", "x", "<p>x</p>", null, "A good meta description.", 3),
                    new StoryTranslationEditDto("pt-PT", "Vistas do Douro", "x", "<p>x</p>", null, null, 3), // present, no meta
                    new StoryTranslationEditDto("es", "", "", "", null, null, 0), // missing translation
                    new StoryTranslationEditDto("fr", "", "", "", null, null, 0), // missing translation
                    new StoryTranslationEditDto("de", "Douro-Blick", "x", "<p>x</p>", null, "Eine Beschreibung.", 3),
                ],
                PreviousSlugs: []),
        };
        var stories = new FakeStoryRead(list, edits);

        var warnings = await new SeoWarningsService(media, stories).GetWarningsAsync(CancellationToken.None);

        // Alt text: only the uncovered image, missing ES/FR/DE.
        warnings.Should().ContainSingle(w => w.Category == "Alt text")
            .Which.Message.Should().Contain("douro-sunset.jpg").And.Contain("ES, FR, DE");

        // Story translation: the published story is missing ES + FR (the draft is ignored).
        warnings.Should().ContainSingle(w => w.Category == "Story translation")
            .Which.Message.Should().Contain("ES, FR");

        // Story meta: only pt-PT (present but no description); en/de have one, es/fr are missing translations.
        warnings.Should().ContainSingle(w => w.Category == "Story meta")
            .Which.Message.Should().Contain("(PT)");
    }

    [Fact]
    public async Task Reports_nothing_when_everything_is_covered()
    {
        var media = new FakeMediaRead(
        [
            new MediaLibraryItemDto(Guid.NewGuid(), MediaKind.Image, "/media/ok-1600.jpg", null, 1600, 900, 1000, Now, "owner",
                LanguagesWithAlt: ["en", "pt-PT", "es", "fr", "de"], ReadyForPublic: true, UsageCount: 0, IsDeleted: false),
        ]);
        var stories = new FakeStoryRead([], new Dictionary<Guid, StoryEditDto>());

        (await new SeoWarningsService(media, stories).GetWarningsAsync(CancellationToken.None)).Should().BeEmpty();
    }

    private sealed class FakeMediaRead(IReadOnlyList<MediaLibraryItemDto> items) : IAdminMediaReadService
    {
        public Task<IReadOnlyList<MediaLibraryItemDto>> GetLibraryAsync(MediaKind? kind, bool includeDeleted, CancellationToken cancellationToken)
            => Task.FromResult(items);

        public Task<MediaAssetDetailDto?> GetAssetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult<MediaAssetDetailDto?>(null);

        public Task<bool> IsReadyForPublicAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(true);

        public Task<GalleryManagerDto?> GetCollectionAsync(string key, CancellationToken cancellationToken)
            => Task.FromResult<GalleryManagerDto?>(null);
    }

    private sealed class FakeStoryRead(IReadOnlyList<StoryListItemDto> list, Dictionary<Guid, StoryEditDto> edits) : IAdminStoryReadService
    {
        public Task<IReadOnlyList<StoryListItemDto>> GetAllAsync(CancellationToken cancellationToken)
            => Task.FromResult(list);

        public Task<StoryEditDto?> GetForEditAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(edits.TryGetValue(id, out var edit) ? edit : null);
    }
}
