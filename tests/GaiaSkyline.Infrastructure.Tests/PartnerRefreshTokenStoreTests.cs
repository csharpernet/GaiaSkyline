using FluentAssertions;
using GaiaSkyline.Infrastructure.Partners;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class PartnerRefreshTokenStoreTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Rotate_chains_and_the_old_token_cannot_be_reused()
    {
        var userId = Guid.NewGuid();
        await using var context = _fixture.CreateContext();
        var store = new PartnerRefreshTokenStore(context, TimeProvider.System);

        var issued = await store.IssueAsync(userId, CancellationToken.None);
        var rotated = await store.RotateAsync(issued.RawToken, CancellationToken.None);

        rotated.Should().NotBeNull();
        rotated!.UserId.Should().Be(userId);
        rotated.RawToken.Should().NotBe(issued.RawToken);

        // The consumed token is no longer usable.
        var reuse = await store.RotateAsync(issued.RawToken, CancellationToken.None);
        reuse.Should().BeNull();
    }

    [Fact]
    public async Task Reusing_a_rotated_token_revokes_the_whole_family()
    {
        var userId = Guid.NewGuid();
        await using var context = _fixture.CreateContext();
        var store = new PartnerRefreshTokenStore(context, TimeProvider.System);

        var issued = await store.IssueAsync(userId, CancellationToken.None);
        var successor = await store.RotateAsync(issued.RawToken, CancellationToken.None);
        successor.Should().NotBeNull();

        // Replaying the already-rotated original is treated as theft: revoke the family.
        var replay = await store.RotateAsync(issued.RawToken, CancellationToken.None);
        replay.Should().BeNull();

        // The (previously valid) successor is now revoked too.
        var successorRotate = await store.RotateAsync(successor!.RawToken, CancellationToken.None);
        successorRotate.Should().BeNull();
    }

    [Fact]
    public async Task Unknown_token_returns_null()
    {
        await using var context = _fixture.CreateContext();
        var store = new PartnerRefreshTokenStore(context, TimeProvider.System);

        (await store.RotateAsync("not-a-real-token", CancellationToken.None)).Should().BeNull();
    }
}
