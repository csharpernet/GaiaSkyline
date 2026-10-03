using FluentAssertions;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Infrastructure.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using GaiaSkyline.Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class DailyRateServiceTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;

    private static readonly DateOnly D1 = new(2027, 5, 1);
    private static readonly DateOnly D2 = new(2027, 5, 2);

    public DailyRateServiceTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        // The class fixture shares one database across the class; start each test from a clean table.
        using var context = _fixture.CreateContext();
        context.DailyRates.ExecuteDelete();
    }

    [Fact]
    public async Task Csv_preview_is_a_dry_run_that_persists_nothing()
    {
        await using var context = _fixture.CreateContext();
        var (service, _) = Build(context);

        var preview = await service.PreviewCsvAsync("date,price,min_nights\n2027-05-01,120,2\nbogus-row", CancellationToken.None);

        preview.Should().HaveCount(2);
        preview.Should().ContainSingle(r => r.Status == "set" && r.Date == D1 && r.Price == 120m && r.MinNights == 2);
        preview.Should().ContainSingle(r => r.Status.StartsWith("invalid", StringComparison.Ordinal));
        (await context.DailyRates.CountAsync()).Should().Be(0); // nothing written
    }

    [Fact]
    public async Task Csv_apply_writes_valid_rows_skips_invalid_and_invalidates_caches()
    {
        var revision = new ContentRevision();
        var before = revision.Current;

        await using (var context = _fixture.CreateContext())
        {
            var (service, _) = Build(context, revision);
            var applied = await service.ApplyCsvAsync("2027-05-01,120,2\n2027-05-02,0\nbad", "owner", CancellationToken.None);
            applied.Should().Be(1); // only the first row is valid (price 0 and 'bad' are skipped)
        }

        await using var verify = _fixture.CreateContext();
        (await verify.DailyRates.CountAsync()).Should().Be(1);
        (await verify.DailyRates.SingleAsync()).NightlyRate.Amount.Should().Be(120m);
        revision.Current.Should().BeGreaterThan(before); // caches invalidated
    }

    [Fact]
    public async Task Set_range_then_clear_range_round_trips()
    {
        await using (var context = _fixture.CreateContext())
        {
            var (service, _) = Build(context);
            await service.SetRangeAsync(D1, D2, 100m, 2, "owner", CancellationToken.None);
        }

        await using (var context = _fixture.CreateContext())
        {
            (await context.DailyRates.CountAsync()).Should().Be(2);
            var (service, _) = Build(context);
            var cleared = await service.ClearRangeAsync(D1, D2, "owner", CancellationToken.None);
            cleared.Should().Be(2);
        }

        await using var verify = _fixture.CreateContext();
        (await verify.DailyRates.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Lock_range_marks_existing_rates_locked()
    {
        await using (var context = _fixture.CreateContext())
        {
            var (service, _) = Build(context);
            await service.SetRangeAsync(D1, D1, 100m, null, "owner", CancellationToken.None);
            var locked = await service.SetLockedRangeAsync(D1, D1, true, "owner", CancellationToken.None);
            locked.Should().Be(1);
        }

        await using var verify = _fixture.CreateContext();
        (await verify.DailyRates.SingleAsync(d => d.Date == D1)).IsLockedByOwner.Should().BeTrue();
    }

    private static (DailyRateService Service, ContentRevision Revision) Build(AppDbContext context, ContentRevision? revision = null)
    {
        revision ??= new ContentRevision();
        var availability = new AvailabilityService(context, new MemoryCache(new MemoryCacheOptions()), new AvailabilityCacheState());
        return (new DailyRateService(context, revision, availability, TimeProvider.System), revision);
    }
}
