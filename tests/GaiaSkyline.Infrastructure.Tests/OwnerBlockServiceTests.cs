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
