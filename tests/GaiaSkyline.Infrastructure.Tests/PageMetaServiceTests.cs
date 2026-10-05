using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Infrastructure.Seo;
using Microsoft.Extensions.Logging.Abstractions;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class PageMetaServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Upsert_sets_then_updates_then_clears_and_the_resolver_reads_the_current_value()
    {
        await using var ctx = _fixture.CreateContext();
        var admin = new PageMetaAdminService(ctx, new ContentRevision(), TimeProvider.System);
        var resolver = new PageMetaResolver(ctx, NullLogger<PageMetaResolver>.Instance);

        // Set.
        (await admin.UpsertAsync("gallery", "en", "Photos of the Douro", "A tour of the balcony view.", "owner", CancellationToken.None))
            .Should().BeTrue();
        var set = await resolver.ResolveAsync("gallery", "en", CancellationToken.None);
        set!.Title.Should().Be("Photos of the Douro");
        set.Description.Should().Be("A tour of the balcony view.");

        // Update.
        await admin.UpsertAsync("gallery", "en", "New title", null, "owner", CancellationToken.None);
        (await resolver.ResolveAsync("gallery", "en", CancellationToken.None))!.Title.Should().Be("New title");

        // Clear (both blank) removes the row.
        await admin.UpsertAsync("gallery", "en", "  ", "  ", "owner", CancellationToken.None);
        (await resolver.ResolveAsync("gallery", "en", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task Upsert_rejects_an_unknown_page_or_language()
    {
        await using var ctx = _fixture.CreateContext();
        var admin = new PageMetaAdminService(ctx, new ContentRevision(), TimeProvider.System);

        (await admin.UpsertAsync("not-a-page", "en", "x", null, "owner", CancellationToken.None)).Should().BeFalse();
        (await admin.UpsertAsync("home", "xx", "x", null, "owner", CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task Resolver_matches_the_exact_language_only()
    {
        await using var ctx = _fixture.CreateContext();
        var admin = new PageMetaAdminService(ctx, new ContentRevision(), TimeProvider.System);
        var resolver = new PageMetaResolver(ctx, NullLogger<PageMetaResolver>.Instance);

        await admin.UpsertAsync("book", "fr", "Réservez", null, "owner", CancellationToken.None);

        (await resolver.ResolveAsync("book", "fr", CancellationToken.None))!.Title.Should().Be("Réservez");
        (await resolver.ResolveAsync("book", "de", CancellationToken.None)).Should().BeNull("no fallback to another language");
    }
}
