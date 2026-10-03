using FluentAssertions;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Domain.Auditing;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Auditing;
using GaiaSkyline.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class AuditReadStoreTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;

    public AuditReadStoreTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        using var context = _fixture.CreateContext();
        context.AuditEvents.ExecuteDelete();
    }

    [Fact]
    public async Task Filters_by_action_and_returns_newest_first()
    {
        await using (var seed = _fixture.CreateContext())
        {
            seed.AuditEvents.Add(new AuditEvent(AuditEventId.New(), new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc), null, "1.1.1.1", "login.success"));
            seed.AuditEvents.Add(new AuditEvent(AuditEventId.New(), new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc), null, "1.1.1.1", "content.publish", "ContentBlock", "home.hero"));
            seed.AuditEvents.Add(new AuditEvent(AuditEventId.New(), new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Utc), null, "1.1.1.1", "login.success"));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var store = new AuditReadStore(context);

        var all = await store.QueryAsync(new AuditLogQuery(), CancellationToken.None);
        all.TotalCount.Should().Be(3);
        all.Items.Should().BeInDescendingOrder(i => i.UtcAt);
        all.Actions.Should().Contain(["login.success", "content.publish"]);

        var filtered = await store.QueryAsync(new AuditLogQuery(Action: "login.success"), CancellationToken.None);
        filtered.TotalCount.Should().Be(2);
        filtered.Items.Should().OnlyContain(i => i.Action == "login.success");
    }

    [Fact]
    public async Task Pages_results()
    {
        await using (var seed = _fixture.CreateContext())
        {
            for (var day = 1; day <= 3; day++)
            {
                seed.AuditEvents.Add(new AuditEvent(AuditEventId.New(), new DateTime(2026, 2, day, 8, 0, 0, DateTimeKind.Utc), null, null, "x.event"));
            }

            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var store = new AuditReadStore(context);

        var page1 = await store.QueryAsync(new AuditLogQuery(Page: 1, PageSize: 2), CancellationToken.None);
        page1.Items.Should().HaveCount(2);
        page1.TotalCount.Should().Be(3);

        var page2 = await store.QueryAsync(new AuditLogQuery(Page: 2, PageSize: 2), CancellationToken.None);
        page2.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task Resolves_the_actor_email_from_the_user_id()
    {
        var userId = Guid.NewGuid();
        await using (var seed = _fixture.CreateContext())
        {
            seed.Set<ApplicationUser>().Add(new ApplicationUser
            {
                Id = userId,
                UserName = "owner@test",
                NormalizedUserName = "OWNER@TEST",
                Email = "owner@test",
                NormalizedEmail = "OWNER@TEST",
                SecurityStamp = Guid.NewGuid().ToString(),
                PreferredLanguage = "en",
                CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            seed.AuditEvents.Add(new AuditEvent(AuditEventId.New(), new DateTime(2026, 3, 1, 8, 0, 0, DateTimeKind.Utc), userId, "1.1.1.1", "2fa.success"));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var store = new AuditReadStore(context);

        var result = await store.QueryAsync(new AuditLogQuery(Action: "2fa.success"), CancellationToken.None);
        result.Items.Should().ContainSingle();
        result.Items[0].ActorEmail.Should().Be("owner@test");
    }
}
