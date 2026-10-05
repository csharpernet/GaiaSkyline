using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Infrastructure.Seo;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class RedirectAdminServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    // Unique path prefix per test so the shared Redirects table doesn't cross-contaminate.
    private static string P() => "/t/" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task Add_then_list_and_delete()
    {
        var p = P();
        await using var ctx = _fixture.CreateContext();
        var service = new RedirectAdminService(ctx, new ContentRevision(), TimeProvider.System);

        var added = await service.AddAsync($"{p}/old", $"{p}/new", permanent: true, "owner", CancellationToken.None);
        added.Ok.Should().BeTrue(added.Error);

        var all = await service.GetAllAsync(CancellationToken.None);
        all.Should().Contain(r => r.FromPath == $"{p}/old" && r.ToPath == $"{p}/new" && r.Permanent);

        (await service.DeleteAsync(added.Id!.Value, "owner", CancellationToken.None)).Should().BeTrue();
        (await service.GetAllAsync(CancellationToken.None)).Should().NotContain(r => r.Id == added.Id);
    }

    [Fact]
    public async Task Rejects_a_duplicate_from_path_case_insensitively()
    {
        var p = P();
        await using var ctx = _fixture.CreateContext();
        var service = new RedirectAdminService(ctx, new ContentRevision(), TimeProvider.System);

        (await service.AddAsync($"{p}/dup", $"{p}/a", true, "owner", CancellationToken.None)).Ok.Should().BeTrue();
        var again = await service.AddAsync($"{p.ToUpperInvariant()}/DUP", $"{p}/b", true, "owner", CancellationToken.None);
        again.Ok.Should().BeFalse();
        again.Error.Should().Contain("already exists");
    }

    [Fact]
    public async Task Rejects_a_direct_loop()
    {
        var p = P();
        await using var ctx = _fixture.CreateContext();
        var service = new RedirectAdminService(ctx, new ContentRevision(), TimeProvider.System);

        (await service.AddAsync($"{p}/a", $"{p}/b", true, "owner", CancellationToken.None)).Ok.Should().BeTrue();
        var loop = await service.AddAsync($"{p}/b", $"{p}/a", true, "owner", CancellationToken.None);
        loop.Ok.Should().BeFalse();
        loop.Error.Should().Contain("loop");
    }

    [Fact]
    public async Task Rejects_an_indirect_chain_loop_but_allows_a_non_looping_chain()
    {
        var p = P();
        await using var ctx = _fixture.CreateContext();
        var service = new RedirectAdminService(ctx, new ContentRevision(), TimeProvider.System);

        (await service.AddAsync($"{p}/a", $"{p}/b", true, "owner", CancellationToken.None)).Ok.Should().BeTrue();
        (await service.AddAsync($"{p}/b", $"{p}/c", true, "owner", CancellationToken.None)).Ok.Should().BeTrue("a → b → c has no cycle");

        var loop = await service.AddAsync($"{p}/c", $"{p}/a", true, "owner", CancellationToken.None);
        loop.Ok.Should().BeFalse("c → a closes the a → b → c → a cycle");
    }
}
