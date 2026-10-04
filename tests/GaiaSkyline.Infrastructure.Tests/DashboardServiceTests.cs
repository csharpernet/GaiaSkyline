using FluentAssertions;
using GaiaSkyline.Application.Admin;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Admin;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class DashboardServiceTests : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture;
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly June = new(2026, 6, 1);

    public DashboardServiceTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        using var context = _fixture.CreateContext();
        context.BookingDateOccupancies.ExecuteDelete();
        context.Bookings.ExecuteDelete();
        context.OwnerBlocks.ExecuteDelete();
        context.ExternalCalendarBlocks.ExecuteDelete();
        context.ExternalCalendarSources.ExecuteDelete();
    }

    [Fact]
    public async Task Manual_sync_todo_lists_confirmed_and_confirmed_then_cancelled_not_unconfirmed_cancellations()
    {
        await using (var seed = _fixture.CreateContext())
        {
            // A: confirmed 48h ago, not mirrored → Block, overdue.
            seed.Bookings.Add(Confirmed("GS-A001", new DateOnly(2026, 8, 1), confirmedAt: Now.AddHours(-48)));
            // B: confirmed then cancelled 1h ago → Unblock, not overdue.
            var b = Confirmed("GS-B002", new DateOnly(2026, 8, 10), confirmedAt: Now.AddHours(-72));
            b.Cancel("guest", Now.AddHours(-1));
            seed.Bookings.Add(b);
            // C: cancelled from AwaitingPayment (never confirmed) → not a to-do.
            var c = AwaitingPayment("GS-C003", new DateOnly(2026, 8, 20));
            c.Cancel("expired", Now.AddHours(-2));
            seed.Bookings.Add(c);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var items = await Service(context).GetOutstandingManualSyncAsync(CancellationToken.None);

        items.Select(i => i.Reference).Should().BeEquivalentTo(["GS-A001", "GS-B002"]);
        items.Single(i => i.Reference == "GS-A001").Action.Should().Be(ManualSyncAction.BlockInHostify);
        items.Single(i => i.Reference == "GS-A001").Overdue.Should().BeTrue();
        items.Single(i => i.Reference == "GS-B002").Action.Should().Be(ManualSyncAction.UnblockInHostify);
        items.Single(i => i.Reference == "GS-B002").Overdue.Should().BeFalse();
    }

    [Fact]
    public async Task Mark_synced_clears_the_item()
    {
        await using (var seed = _fixture.CreateContext())
        {
            seed.Bookings.Add(Confirmed("GS-S001", new DateOnly(2026, 8, 1), confirmedAt: Now.AddHours(-48)));
            await seed.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateContext())
        {
            var service = Service(context);
            (await service.GetOutstandingManualSyncAsync(CancellationToken.None)).Should().ContainSingle();
            await service.MarkSyncedAsync("GS-S001", "blocked in Hostify", null, "1.1.1.1", CancellationToken.None);
        }

        await using var verify = _fixture.CreateContext();
        (await Service(verify).GetOutstandingManualSyncAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task To_do_is_empty_when_an_external_calendar_is_connected()
    {
        await using (var seed = _fixture.CreateContext())
        {
            seed.Bookings.Add(Confirmed("GS-E001", new DateOnly(2026, 8, 1), confirmedAt: Now.AddHours(-48)));
            seed.ExternalCalendarSources.Add(new ExternalCalendarSource(
                ExternalCalendarSourceId.New(), "Hostify", "protected-url", isEnabled: true, Now));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        (await Service(context).GetOutstandingManualSyncAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Calendar_and_kpis_reflect_bookings_and_owner_blocks()
    {
        await using (var seed = _fixture.CreateContext())
        {
            // A confirmed stay this month: 16–19 June (3 nights), revenue 300.
            var booking = Confirmed("GS-K001", new DateOnly(2026, 6, 16), confirmedAt: Now, total: 300m);
            seed.Bookings.Add(booking);
            for (var d = new DateOnly(2026, 6, 16); d < new DateOnly(2026, 6, 19); d = d.AddDays(1))
            {
                seed.BookingDateOccupancies.Add(new BookingDateOccupancy(d, booking.Id));
            }

            // Owner unavailable 20–22 June (exclusive end).
            seed.OwnerBlocks.Add(new OwnerBlock(OwnerBlockId.New(), new DateOnly(2026, 6, 20), new DateOnly(2026, 6, 22),
                OwnerBlockKind.OwnerUnavailable, "maintenance", Now, "owner"));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var summary = await Service(context).GetAsync(June, CancellationToken.None);

        summary.BookingsThisMonth.Should().Be(1);
        summary.RevenueMonthToDate.Should().Be(300m);
        summary.NextCheckIn!.Reference.Should().Be("GS-K001");
        summary.CalendarManualMode.Should().BeTrue();
        summary.PricingManualMode.Should().BeTrue();

        DayOccupancy OccOf(DateOnly date) => summary.CalendarDays.Single(c => c.Date == date).Occupancy;
        OccOf(new DateOnly(2026, 6, 16)).Should().Be(DayOccupancy.DirectBooking);
        OccOf(new DateOnly(2026, 6, 18)).Should().Be(DayOccupancy.DirectBooking);
        OccOf(new DateOnly(2026, 6, 19)).Should().Be(DayOccupancy.Free); // checkout day is free
        OccOf(new DateOnly(2026, 6, 20)).Should().Be(DayOccupancy.OwnerUnavailable);
        OccOf(new DateOnly(2026, 6, 21)).Should().Be(DayOccupancy.OwnerUnavailable);
        summary.OccupancyPercent.Should().BeGreaterThan(0);
    }

    private static DashboardService Service(AppDbContext context) =>
        new(context, Options.Create(new PricingProviderOptions()), new NoOpAuditLog(), new FixedClock(Now));

    private static Money Eur(decimal amount) => new(amount, "EUR");

    private static Booking AwaitingPayment(string reference, DateOnly checkIn, decimal total = 300m) =>
        new(BookingId.New(), reference, checkIn, checkIn.AddDays(3), 2, 0, 0,
            "Guest", "g@example.com", "+351000000000", "PT", "en",
            Eur(100m), Eur(total), Eur(0m), Eur(0m), Eur(0m), Eur(total), Now.AddDays(-5));

    private static Booking Confirmed(string reference, DateOnly checkIn, DateTime confirmedAt, decimal total = 300m)
    {
        var booking = AwaitingPayment(reference, checkIn, total);
        booking.ConfirmPayment("card", confirmedAt);
        return booking;
    }

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class NoOpAuditLog : IAuditLog
    {
        public Task WriteAsync(string action, Guid? actorUserId, string? actorIp, string? entityType = null,
            string? entityId = null, object? details = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
