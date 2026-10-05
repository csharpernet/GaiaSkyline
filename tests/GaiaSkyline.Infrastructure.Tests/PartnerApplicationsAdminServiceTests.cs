using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Infrastructure.Partners;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>Partner-application review (Stage 7 §11): pending-first list and the one-shot decision.</summary>
public sealed class PartnerApplicationsAdminServiceTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;

    public PartnerApplicationsAdminServiceTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        using var context = _fixture.CreateContext();
        context.PartnerApplications.ExecuteDelete();
    }

    private static PartnerApplication Application(string name, string email, DateTime submittedAtUtc) => new(
        PartnerApplicationId.New(), name, email, "https://instagram.com/x", 10_000, "Travel", "Hi!", submittedAtUtc);

    [Fact]
    public async Task Lists_pending_first_then_newest_and_decides_exactly_once()
    {
        await using var context = _fixture.CreateContext();
        var older = Application("Older Pending", "older@x.com", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = Application("Newer Pending", "newer@x.com", new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
        var decided = Application("Already Decided", "done@x.com", new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc));
        decided.Approve(new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc), null);
        context.PartnerApplications.AddRange(older, newer, decided);
        await context.SaveChangesAsync();

        var service = new PartnerApplicationsAdminService(context, TimeProvider.System);
        var all = await service.GetAllAsync(CancellationToken.None);

        all.Select(a => a.Name).Should().ContainInOrder("Newer Pending", "Older Pending", "Already Decided");

        (await service.RejectAsync(newer.Id.Value, "audience too small", CancellationToken.None)).Ok.Should().BeTrue();
        var rejected = (await service.GetAllAsync(CancellationToken.None)).Single(a => a.Name == "Newer Pending");
        rejected.Status.Should().Be("Rejected");
        rejected.DecisionNote.Should().Be("audience too small");
        rejected.DecidedAtUtc.Should().NotBeNull();

        // A decided application never flips.
        (await service.ApproveAsync(newer.Id.Value, null, CancellationToken.None)).Ok.Should().BeFalse();
        (await service.ApproveAsync(Guid.NewGuid(), null, CancellationToken.None)).Ok.Should().BeFalse("unknown id");
    }

    [Fact]
    public void The_entity_validates_its_inputs()
    {
        var blankName = () => Application(" ", "a@b.com", DateTime.UtcNow);
        blankName.Should().Throw<ArgumentException>();
        var negativeAudience = () => new PartnerApplication(
            PartnerApplicationId.New(), "N", "a@b.com", null, -1, null, null, DateTime.UtcNow);
        negativeAudience.Should().Throw<ArgumentOutOfRangeException>();
    }
}
