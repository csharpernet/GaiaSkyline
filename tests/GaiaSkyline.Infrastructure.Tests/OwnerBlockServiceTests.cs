using FluentAssertions;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Availability;
using GaiaSkyline.Infrastructure.Bookings;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class OwnerBlockServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    private static (AvailabilityService Availability, OwnerBlockService Service) Build(Persistence.AppDbContext context)
    {
        var availability = new AvailabilityService(context, new MemoryCache(new MemoryCacheOptions()), new AvailabilityCacheState());
        var service = new OwnerBlockService(context, availability, new IcsCacheInvalidator(), TimeProvider.System);
        return (availability, service);
    }

    [Fact]
    public async Task Created_block_appears_in_availability_with_exclusive_end()
    {
        await using var context = _fixture.CreateContext();
        var (availability, service) = Build(context);

        // Exclusive end: nights 10, 11, 12 are blocked; 13 is free again.
        await service.CreateAsync(new DateOnly(2028, 3, 10), new DateOnly(2028, 3, 13),
            OwnerBlockKind.OwnerUnavailable, "maintenance", "owner", CancellationToken.None);

        var blocked = await availability.GetBlockedDatesAsync(new DateOnly(2028, 3, 1), new DateOnly(2028, 4, 1), CancellationToken.None);

        blocked.Should().Contain([new DateOnly(2028, 3, 10), new DateOnly(2028, 3, 11), new DateOnly(2028, 3, 12)]);
        blocked.Should().NotContain(new DateOnly(2028, 3, 13));
    }

    [Fact]
    public async Task Duplicate_report_lists_external_booking_blocks_matching_an_imported_block()
    {
        await using var context = _fixture.CreateContext();
        // ExternalBooking owner block [1 Jul, 4 Jul) — a manual copy of a Hostify reservation.
        context.OwnerBlocks.Add(new OwnerBlock(OwnerBlockId.New(), new DateOnly(2029, 7, 1), new DateOnly(2029, 7, 4),
            OwnerBlockKind.ExternalBooking, "Hostify copy", DateTime.UtcNow, "owner"));
        // A non-matching ExternalBooking block.
        context.OwnerBlocks.Add(new OwnerBlock(OwnerBlockId.New(), new DateOnly(2029, 8, 1), new DateOnly(2029, 8, 4),
            OwnerBlockKind.ExternalBooking, "other", DateTime.UtcNow, "owner"));
        // The imported block (inclusive end 3 Jul) matches the first owner block (exclusive end 4 Jul).
        context.ExternalCalendarBlocks.Add(new ExternalCalendarBlock(
            ExternalCalendarBlockId.New(), "Hostify", new DateOnly(2029, 7, 1), new DateOnly(2029, 7, 3), DateTime.UtcNow, "h1@hostify.com"));
        await context.SaveChangesAsync();

        var (_, service) = Build(context);

        var duplicates = await service.ListImportedDuplicatesAsync(CancellationToken.None);

        duplicates.Should().ContainSingle(d => d.StartDate == new DateOnly(2029, 7, 1) && d.EndDate == new DateOnly(2029, 7, 4));
    }

    [Fact]
    public async Task Update_reschedules_a_block_but_rejects_overlapping_an_active_booking()
    {
        await using var context = _fixture.CreateContext();
        var booking = new Booking(
            BookingId.New(), "GS-OB02", new DateOnly(2028, 6, 10), new DateOnly(2028, 6, 13),
            2, 0, 0, "Guest", "g2@example.com", "+351 912 345 678", "Portugal", "en",
            new Money(100m, "EUR"), new Money(300m, "EUR"), new Money(0m, "EUR"),
            new Money(60m, "EUR"), new Money(0m, "EUR"), new Money(360m, "EUR"), DateTime.UtcNow);
        booking.ConfirmPayment("card", DateTime.UtcNow);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var (availability, service) = Build(context);
        var id = await service.CreateAsync(new DateOnly(2028, 6, 1), new DateOnly(2028, 6, 3),
            OwnerBlockKind.ExternalBooking, "original", "owner", CancellationToken.None);

        // Happy path: new dates + note stick, and availability reflects the move.
        (await service.UpdateAsync(id, new DateOnly(2028, 6, 4), new DateOnly(2028, 6, 6), "moved", CancellationToken.None))
            .Should().BeTrue();
        var blocked = await availability.GetBlockedDatesAsync(new DateOnly(2028, 6, 1), new DateOnly(2028, 7, 1), CancellationToken.None);
        blocked.Should().Contain(new DateOnly(2028, 6, 4)).And.NotContain(new DateOnly(2028, 6, 1));
        (await service.ListAsync(new DateOnly(2028, 6, 1), new DateOnly(2028, 7, 1), CancellationToken.None))
            .Should().ContainSingle(b => b.Note == "moved");

        // Overlapping the booking is rejected with its reference; unknown ids return false.
        var act = async () => await service.UpdateAsync(id, new DateOnly(2028, 6, 11), new DateOnly(2028, 6, 12), null, CancellationToken.None);
        (await act.Should().ThrowAsync<OwnerBlockConflictsWithBookingException>())
            .Which.BookingReference.Should().Be("GS-OB02");
        (await service.UpdateAsync(Guid.NewGuid(), new DateOnly(2028, 6, 1), new DateOnly(2028, 6, 2), null, CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Overlapping_an_active_booking_is_rejected_naming_the_reference()
    {
        await using var context = _fixture.CreateContext();
        var booking = new Booking(
            BookingId.New(), "GS-OB01", new DateOnly(2028, 5, 1), new DateOnly(2028, 5, 5),
            2, 0, 0, "Guest", "g@example.com", "+351 912 345 678", "Portugal", "en",
            new Money(100m, "EUR"), new Money(400m, "EUR"), new Money(0m, "EUR"),
            new Money(60m, "EUR"), new Money(0m, "EUR"), new Money(460m, "EUR"), DateTime.UtcNow);
        booking.ConfirmPayment("card", DateTime.UtcNow);
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var (_, service) = Build(context);

        var act = async () => await service.CreateAsync(
            new DateOnly(2028, 5, 3), new DateOnly(2028, 5, 7), OwnerBlockKind.OwnerUnavailable, null, "owner", CancellationToken.None);

        (await act.Should().ThrowAsync<OwnerBlockConflictsWithBookingException>())
            .Which.BookingReference.Should().Be("GS-OB01");
    }
}
