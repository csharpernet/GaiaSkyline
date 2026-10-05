using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Reviews;
using GaiaSkyline.Infrastructure.Content;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>Review admin CRUD (Stage 7 §10): list, off-platform add, typo edit, publish toggle, revision bumps.</summary>
public sealed class ReviewsAdminServiceTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;

    public ReviewsAdminServiceTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        using var context = _fixture.CreateContext();
        context.Reviews.ExecuteDelete();
    }

    private static ReviewWriteModel Review(string name = "Rita", int rating = 5, string body = "Wonderful stay.") =>
        new(rating, name, "Porto", body, "Google", new DateOnly(2026, 8, 10));

    [Fact]
    public async Task Create_edit_and_publish_toggle_round_trip_and_bump_the_revision()
    {
        await using var context = _fixture.CreateContext();
        var revision = new ContentRevision();
        var service = new ReviewsAdminService(context, revision);
        var before = revision.Current;

        (await service.CreateAsync(Review(), publish: false, CancellationToken.None)).Ok.Should().BeTrue();
        var created = (await service.GetAllAsync(CancellationToken.None)).Single();
        created.IsPublished.Should().BeFalse();
        revision.Current.Should().BeGreaterThan(before, "the home cache must refresh");

        (await service.UpdateAsync(created.Id, Review(body: "Wonderful stay — fixed typo."), CancellationToken.None))
            .Ok.Should().BeTrue();
        (await service.SetPublishedAsync(created.Id, true, CancellationToken.None)).Ok.Should().BeTrue();

        var updated = (await service.GetAllAsync(CancellationToken.None)).Single();
        updated.Body.Should().Contain("fixed typo");
        updated.IsPublished.Should().BeTrue();

        // Only published reviews reach the public read path.
        (await context.Reviews.CountAsync(r => r.IsPublished)).Should().Be(1);
        (await service.SetPublishedAsync(created.Id, false, CancellationToken.None)).Ok.Should().BeTrue();
        (await context.Reviews.CountAsync(r => r.IsPublished)).Should().Be(0);
    }

    [Fact]
    public async Task Invalid_input_fails_cleanly_without_writing()
    {
        await using var context = _fixture.CreateContext();
        var service = new ReviewsAdminService(context, new ContentRevision());

        (await service.CreateAsync(Review(rating: 6), publish: true, CancellationToken.None)).Ok.Should().BeFalse();
        (await service.CreateAsync(Review(name: " "), publish: true, CancellationToken.None)).Ok.Should().BeFalse();
        (await service.UpdateAsync(Guid.NewGuid(), Review(), CancellationToken.None)).Ok.Should().BeFalse();
        (await service.SetPublishedAsync(Guid.NewGuid(), true, CancellationToken.None)).Ok.Should().BeFalse();
        (await context.Reviews.CountAsync()).Should().Be(0);
    }
}
