using FluentAssertions;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Availability;
using GaiaSkyline.Infrastructure.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class GuestCancellationTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Cancelling_releases_the_dates_and_regenerates_the_export()
    {
        var bookingId = BookingId.New();
        var checkIn = new DateOnly(2028, 9, 1);
        var checkOut = new DateOnly(2028, 9, 4);

        await using (var seed = _fixture.CreateContext())
        {
            var booking = new Booking(
                bookingId, "GS-CX1", checkIn, checkOut, 2, 0, 0, "Guest", "g@example.com",
                "+351 912 345 678", "Portugal", "en", new Money(100m, "EUR"), new Money(300m, "EUR"),
                new Money(0m, "EUR"), new Money(60m, "EUR"), new Money(0m, "EUR"), new Money(360m, "EUR"), DateTime.UtcNow);
            booking.ConfirmPayment("card", DateTime.UtcNow);
            seed.Bookings.Add(booking);
            for (var night = checkIn; night < checkOut; night = night.AddDays(1))
            {
                seed.BookingDateOccupancies.Add(new BookingDateOccupancy(night, bookingId));
            }

            await seed.SaveChangesAsync();
        }

        var ics = new IcsCacheInvalidator();
        var versionBefore = ics.Version;

        await using var context = _fixture.CreateContext();
        var availability = new AvailabilityService(context, new MemoryCache(new MemoryCacheOptions()), new AvailabilityCacheState());
        var lifecycle = new BookingLifecycleService(context, availability, ics, TimeProvider.System);

        await lifecycle.CancelAndReleaseAsync(bookingId, "Guest cancellation", CancellationToken.None);

        // Export regenerates (the public .ics feed updates).
        ics.Version.Should().BeGreaterThan(versionBefore);

        await using var verify = _fixture.CreateContext();
        (await verify.BookingDateOccupancies.CountAsync(o => o.BookingId == bookingId)).Should().Be(0);
        (await verify.Bookings.FirstAsync(b => b.Id == bookingId)).Status.Should().Be(BookingStatus.Cancelled);
    }
}
