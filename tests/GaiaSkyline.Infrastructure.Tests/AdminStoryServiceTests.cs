using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Content;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class AdminStoryServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    private static AdminStoryService Service(Persistence.AppDbContext ctx) =>
        new(ctx, new HtmlContentSanitizer(), new ContentRevision());

    private async Task<Guid> SeedCoverAsync()
    {
        var asset = new MediaAsset(
            MediaAssetId.New(), MediaKind.Image, $"/media/{Guid.NewGuid():N}-1600.jpg", null,
            1600, 1066, null, 1, "image/jpeg", DateTime.UtcNow, "seed");
        await using var ctx = _fixture.CreateContext();
        ctx.MediaAssets.Add(asset);
        await ctx.SaveChangesAsync();
        return asset.Id.Value;
    }

    private static StoryWriteModel Model(Guid cover, string enTitle, string? slug = null, bool published = true, string body = "<p>Hello world from the Douro.</p>") =>
        new(slug, "Host", cover, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), published,
            [new StoryTranslationInput("en", enTitle, "An excerpt", body, null, null)]);

    [Fact]
    public async Task Create_derives_a_slug_from_the_title_computes_reading_time_and_sanitises_the_body()
    {
        var cover = await SeedCoverAsync();
        var title = "A Slow Morning " + Guid.NewGuid().ToString("N")[..8];

        Guid id;
        await using (var ctx = _fixture.CreateContext())
        {
            var result = await Service(ctx).CreateAsync(
                Model(cover, title, body: "<p>Keep this.</p><script>evil()</script>"), "owner", CancellationToken.None);
            result.Ok.Should().BeTrue(result.Error);
            id = result.StoryId!.Value;
        }

        await using var verify = _fixture.CreateContext();
        var story = await verify.Stories.Include(s => s.Translations).FirstAsync(s => s.Id == StoryId.From(id));
        story.Slug.Should().Be("a-slow-morning-" + title.Split(' ')[^1].ToLowerInvariant());
        var en = story.Translations.Single();
        en.BodyRichText.Should().Contain("Keep this.").And.NotContain("<script");
        en.ReadingTimeMinutes.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Create_rejects_a_missing_english_title_or_cover()
    {
        var cover = await SeedCoverAsync();
        await using var ctx = _fixture.CreateContext();

        var noTitle = await Service(ctx).CreateAsync(Model(cover, "   "), "owner", CancellationToken.None);
        noTitle.Ok.Should().BeFalse();

        var noCover = await Service(ctx).CreateAsync(Model(Guid.Empty, "Has title"), "owner", CancellationToken.None);
        noCover.Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_titles_get_distinct_slugs()
    {
        var cover = await SeedCoverAsync();
        var title = "Shared Title " + Guid.NewGuid().ToString("N")[..8];

        await using var ctx = _fixture.CreateContext();
        var first = await Service(ctx).CreateAsync(Model(cover, title), "owner", CancellationToken.None);
        var second = await Service(ctx).CreateAsync(Model(cover, title), "owner", CancellationToken.None);

        await using var verify = _fixture.CreateContext();
        var a = await verify.Stories.FirstAsync(s => s.Id == StoryId.From(first.StoryId!.Value));
        var b = await verify.Stories.FirstAsync(s => s.Id == StoryId.From(second.StoryId!.Value));
        a.Slug.Should().NotBe(b.Slug);
        b.Slug.Should().EndWith("-2");
    }

    [Fact]
    public async Task Renaming_a_published_story_records_an_alias_that_the_resolver_resolves()
    {
        var cover = await SeedCoverAsync();
        var title = "Original " + Guid.NewGuid().ToString("N")[..8];

        Guid id;
        string oldSlug, newSlug;
        await using (var ctx = _fixture.CreateContext())
        {
            var created = await Service(ctx).CreateAsync(Model(cover, title, published: true), "owner", CancellationToken.None);
            id = created.StoryId!.Value;
        }

        await using (var read = _fixture.CreateContext())
        {
            oldSlug = (await read.Stories.FirstAsync(s => s.Id == StoryId.From(id))).Slug;
        }

        newSlug = "renamed-" + Guid.NewGuid().ToString("N")[..8];
        await using (var ctx = _fixture.CreateContext())
        {
            var updated = await Service(ctx).UpdateAsync(id, Model(cover, title, slug: newSlug, published: true), "owner", CancellationToken.None);
            updated.Ok.Should().BeTrue(updated.Error);
        }

        await using var verify = _fixture.CreateContext();
        var story = await verify.Stories.Include(s => s.Aliases).FirstAsync(s => s.Id == StoryId.From(id));
        story.Slug.Should().Be(newSlug);
        story.Aliases.Select(a => a.OldSlug).Should().Contain(oldSlug);

        var resolver = new StorySlugRedirectResolver(verify);
        (await resolver.ResolveCurrentSlugAsync(oldSlug, "en", CancellationToken.None)).Should().Be(newSlug);
        (await resolver.ResolveCurrentSlugAsync("never-a-slug", "en", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task A_language_gets_its_own_slug_and_renaming_it_301s_within_that_language()
    {
        var cover = await SeedCoverAsync();
        var title = "Lang " + Guid.NewGuid().ToString("N")[..8];
        var frTitle = "Pont de Dona Maria " + Guid.NewGuid().ToString("N")[..8];

        // Authoring FR for the first time auto-suggests the slug from the FRENCH title (Stage 7 §4).
        Guid id;
        await using (var ctx = _fixture.CreateContext())
        {
            var model = Model(cover, title, published: true) with
            {
                Translations =
                [
                    new StoryTranslationInput("en", title, "Excerpt", "<p>Body</p>", null, null),
                    new StoryTranslationInput("fr", frTitle, "Extrait", "<p>Corps</p>", null, null),
                ],
            };
            var created = await Service(ctx).CreateAsync(model, "owner", CancellationToken.None);
            created.Ok.Should().BeTrue(created.Error);
            id = created.StoryId!.Value;
        }

        string frSlug, enSlug;
        await using (var read = _fixture.CreateContext())
        {
            var story = await read.Stories.Include(s => s.Translations).FirstAsync(s => s.Id == StoryId.From(id));
            enSlug = story.Slug;
            frSlug = story.SlugFor("fr");
        }

        frSlug.Should().StartWith("pont-de-dona-maria").And.NotBe(enSlug);

        // /fr resolves the FR slug; /en does not (slugs are unique per language)...
        await using (var verify = _fixture.CreateContext())
        {
            var store = new ContentReadStore(verify);
            (await store.GetPublishedStoryBySlugAsync(frSlug, "fr", CancellationToken.None)).Should().NotBeNull();
            (await store.GetPublishedStoryBySlugAsync(frSlug, "en", CancellationToken.None)).Should().BeNull();

            // ...and the EN slug asked in FR 301s to the FR slug (cross-language resolve).
            var resolver = new StorySlugRedirectResolver(verify);
            (await resolver.ResolveCurrentSlugAsync(enSlug, "fr", CancellationToken.None)).Should().Be(frSlug);
        }

        // Renaming the published FR slug records a FR-scoped alias the resolver honours only for FR.
        var newFrSlug = "nouveau-pont-" + Guid.NewGuid().ToString("N")[..8];
        await using (var ctx = _fixture.CreateContext())
        {
            var model = Model(cover, title, published: true) with
            {
                Translations =
                [
                    new StoryTranslationInput("en", title, "Excerpt", "<p>Body</p>", null, null),
                    new StoryTranslationInput("fr", frTitle, "Extrait", "<p>Corps</p>", null, null, newFrSlug),
                ],
            };
            (await Service(ctx).UpdateAsync(id, model, "owner", CancellationToken.None)).Ok.Should().BeTrue();
        }

        await using var after = _fixture.CreateContext();
        var renamed = await after.Stories.Include(s => s.Translations).Include(s => s.Aliases)
            .FirstAsync(s => s.Id == StoryId.From(id));
        renamed.SlugFor("fr").Should().Be(newFrSlug);
        renamed.Aliases.Should().ContainSingle(a => a.OldSlug == frSlug && a.LanguageCode == "fr");

        var afterResolver = new StorySlugRedirectResolver(after);
        (await afterResolver.ResolveCurrentSlugAsync(frSlug, "fr", CancellationToken.None)).Should().Be(newFrSlug);
        (await afterResolver.ResolveCurrentSlugAsync(frSlug, "en", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Renaming_a_draft_story_records_no_alias()
    {
        var cover = await SeedCoverAsync();
        var title = "Draft " + Guid.NewGuid().ToString("N")[..8];

        Guid id;
        await using (var ctx = _fixture.CreateContext())
        {
            id = (await Service(ctx).CreateAsync(Model(cover, title, published: false), "owner", CancellationToken.None)).StoryId!.Value;
        }

        await using (var ctx = _fixture.CreateContext())
        {
            await Service(ctx).UpdateAsync(id, Model(cover, title, slug: "draft-renamed-" + Guid.NewGuid().ToString("N")[..8], published: false), "owner", CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        var story = await verify.Stories.Include(s => s.Aliases).FirstAsync(s => s.Id == StoryId.From(id));
        story.Aliases.Should().BeEmpty();
    }

    [Fact]
    public async Task Read_service_lists_stories_and_returns_all_languages_for_edit()
    {
        var cover = await SeedCoverAsync();
        var title = "Readable " + Guid.NewGuid().ToString("N")[..8];
        Guid id;
        await using (var ctx = _fixture.CreateContext())
        {
            id = (await Service(ctx).CreateAsync(Model(cover, title), "owner", CancellationToken.None)).StoryId!.Value;
        }

        await using var ctx2 = _fixture.CreateContext();
        var read = new AdminStoryReadService(ctx2);

        (await read.GetAllAsync(CancellationToken.None)).Should().Contain(s => s.Id == id && s.Title == title);

        var detail = await read.GetForEditAsync(id, CancellationToken.None);
        detail.Should().NotBeNull();
        detail!.Translations.Should().HaveCount(ContentLanguages.All.Count);
        detail.Translations.Single(t => t.LanguageCode == "en").Title.Should().Be(title);
    }
}
