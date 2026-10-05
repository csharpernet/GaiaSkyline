using FluentAssertions;
using GaiaSkyline.Application.Admin;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Admin;
using GaiaSkyline.Infrastructure.Availability;
using GaiaSkyline.Infrastructure.Bookings;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>The admin calendar read model + conflict resolution (Stage 7 §7).</summary>
public sealed class CalendarAdminServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    private static CalendarAdminService Build(Persistence.AppDbContext context)
    {
        var availability = new AvailabilityService(context, new MemoryCache(new MemoryCacheOptions()), new AvailabilityCacheState());
        var ownerBlocks = new OwnerBlockService(context, availability, new IcsCacheInvalidator(), TimeProvider.System);
        return new CalendarAdminService(context, ownerBlocks, TimeProvider.System);
    }

    [Fact]
    public async Task Month_grid_shows_every_kind_with_identity_and_ics_hints()
    {
        await using var context = _fixture.CreateContext();
        var booking = new Booking(
            BookingId.New(), "GS-CAL01", new DateOnly(2027, 6, 10), new DateOnly(2027, 6, 13),
            2, 0, 0, "Grid Guest", "cal@example.com", "+351 912 345 678", "PT", "en",
            new Money(100m, "EUR"), new Money(300m, "EUR"), new Money(0m, "EUR"),
            new Money(0m, "EUR"), new Money(0m, "EUR"), new Money(300m, "EUR"), DateTime.UtcNow);
        booking.ConfirmPayment("card", DateTime.UtcNow);
        context.Bookings.Add(booking);
        context.OwnerBlocks.Add(new OwnerBlock(OwnerBlockId.New(), new DateOnly(2027, 6, 15), new DateOnly(2027, 6, 17),
            OwnerBlockKind.OwnerUnavailable, "hold", DateTime.UtcNow, "owner"));
        context.OwnerBlocks.Add(new OwnerBlock(OwnerBlockId.New(), new DateOnly(2027, 6, 18), new DateOnly(2027, 6, 20),
            OwnerBlockKind.ExternalBooking, "hostify copy", DateTime.UtcNow, "owner"));
        // Imported block with an INCLUSIVE end (21st and 22nd blocked, 23rd free).
        context.ExternalCalendarBlocks.Add(new ExternalCalendarBlock(
            ExternalCalendarBlockId.New(), "Hostify", new DateOnly(2027, 6, 21), new DateOnly(2027, 6, 22), DateTime.UtcNow));
        await context.SaveChangesAsync();

        var period = await Build(context).GetMonthAsync(new DateOnly(2027, 6, 1), CancellationToken.None);

        // Monday-first full-week grid.
        period.Cells.Count.Should().BeGreaterThan(27);
        (period.Cells.Count % 7).Should().Be(0);
        period.Cells[0].Date.DayOfWeek.Should().Be(DayOfWeek.Monday);

        CalendarDayCell Cell(int day) => period.Cells.Single(c => c.Date == new DateOnly(2027, 6, day));

        var bookingItem = Cell(10).Items.Should().ContainSingle(i => i.Kind == CalendarItemKind.DirectBooking).Subject;
        bookingItem.Label.Should().Contain("GS-CAL01").And.Contain("Grid Guest");
        bookingItem.InIcsExport.Should().BeTrue();
        bookingItem.BookingId.Should().NotBeNull();
        Cell(13).Items.Should().NotContain(i => i.Kind == CalendarItemKind.DirectBooking, "check-out day is free");

        var hold = Cell(15).Items.Should().ContainSingle(i => i.Kind == CalendarItemKind.OwnerUnavailable).Subject;
        hold.InIcsExport.Should().BeTrue("OwnerUnavailable blocks are exported over ICS");
        hold.OwnerBlockId.Should().NotBeNull();

        var copy = Cell(18).Items.Should().ContainSingle(i => i.Kind == CalendarItemKind.ExternalOwnerBlock).Subject;
        copy.InIcsExport.Should().BeFalse("ExternalBooking blocks would echo back over iCal");

        Cell(21).Items.Should().ContainSingle(i => i.Kind == CalendarItemKind.ImportedBlock);
        Cell(22).Items.Should().ContainSingle(i => i.Kind == CalendarItemKind.ImportedBlock, "inclusive end night");
        Cell(23).Items.Should().NotContain(i => i.Kind == CalendarItemKind.ImportedBlock);

        period.Blocks.Should().HaveCount(2, "owner blocks in the period, with identity for edit/delete");
        period.ManualMode.Should().BeTrue("no external calendar sources are enabled");
    }

    [Fact]
    public async Task Week_grid_is_exactly_the_monday_week_of_the_anchor()
    {
        await using var context = _fixture.CreateContext();
        var period = await Build(context).GetWeekAsync(new DateOnly(2027, 6, 16), CancellationToken.None);

        period.Cells.Should().HaveCount(7);
        period.Cells[0].Date.Should().Be(new DateOnly(2027, 6, 14), "the Monday of the anchor's week");
        period.Cells.Should().OnlyContain(c => c.InPeriod);
    }

    [Fact]
    public async Task Open_conflicts_link_the_booking_and_resolution_removes_them()
    {
        await using var context = _fixture.CreateContext();
        var booking = new Booking(
            BookingId.New(), "GS-CFL01", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 4),
            2, 0, 0, "Conflicted", "cfl@example.com", "+351 912 345 678", "PT", "en",
            new Money(100m, "EUR"), new Money(300m, "EUR"), new Money(0m, "EUR"),
            new Money(0m, "EUR"), new Money(0m, "EUR"), new Money(300m, "EUR"), DateTime.UtcNow);
        context.Bookings.Add(booking);
        var conflict = new BookingConflict(
            BookingConflictId.New(), "GS-CFL01", "Hostify", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 4), DateTime.UtcNow);
        context.BookingConflicts.Add(conflict);
        await context.SaveChangesAsync();

        var service = Build(context);
        var period = await service.GetMonthAsync(new DateOnly(2027, 7, 1), CancellationToken.None);
        var open = period.OpenConflicts.Should().ContainSingle(c => c.BookingReference == "GS-CFL01").Subject;
        open.BookingId.Should().Be(booking.Id.Value, "the reference resolves to the booking for deep links");

        (await service.ResolveConflictAsync(conflict.Id.Value, "guest moved", CancellationToken.None)).Should().BeTrue();
        (await service.ResolveConflictAsync(Guid.NewGuid(), null, CancellationToken.None)).Should().BeFalse();

        var after = await service.GetMonthAsync(new DateOnly(2027, 7, 1), CancellationToken.None);
        after.OpenConflicts.Should().NotContain(c => c.BookingReference == "GS-CFL01");

        await using var verify = _fixture.CreateContext();
        var stored = verify.BookingConflicts.Single(c => c.BookingReference == "GS-CFL01");
        stored.IsResolved.Should().BeTrue();
        stored.ResolvedNote.Should().Be("guest moved");
    }
}
