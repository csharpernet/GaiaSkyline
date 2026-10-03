using System.Text;
using FluentAssertions;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Availability;
using Microsoft.Extensions.Caching.Memory;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class IcsExportServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;

    [Fact]
    public async Task Export_includes_bookings_and_owner_unavailable_but_excludes_external_bookings()
    {
        var checkIn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);
        var checkOut = checkIn.AddDays(3);
        var unavailableId = OwnerBlockId.New();
        var externalId = OwnerBlockId.New();

        await using (var seed = _fixture.CreateContext())
        {
            var booking = new Booking(
                BookingId.New(), "GS-ICS1", checkIn, checkOut, 2, 0, 0, "Guest", "g@example.com",
                "+351 912 345 678", "Portugal", "en", new Money(100m, "EUR"), new Money(300m, "EUR"),
                new Money(0m, "EUR"), new Money(60m, "EUR"), new Money(0m, "EUR"), new Money(360m, "EUR"), DateTime.UtcNow);
            booking.ConfirmPayment("card", DateTime.UtcNow);
            seed.Bookings.Add(booking);

            seed.OwnerBlocks.Add(new OwnerBlock(unavailableId, checkIn.AddDays(10), checkIn.AddDays(12),
                OwnerBlockKind.OwnerUnavailable, "maintenance", DateTime.UtcNow, "owner"));
            seed.OwnerBlocks.Add(new OwnerBlock(externalId, checkIn.AddDays(30), checkIn.AddDays(32),
                OwnerBlockKind.ExternalBooking, "Hostify copy", DateTime.UtcNow, "owner"));

            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var service = new IcsExportService(context, new MemoryCache(new MemoryCacheOptions()), new IcsCacheInvalidator(), TimeProvider.System);

        var result = await service.GetAsync(CancellationToken.None);
        var ics = Encoding.UTF8.GetString(result.Content);

        ics.Should().Contain("booking-GS-ICS1@gaiaskyline");
        ics.Should().Contain("SUMMARY:Unavailable");
        ics.Should().Contain($"ownerblock-{unavailableId.Value:N}@gaiaskyline");
        // The ExternalBooking block must NOT be exported (it would echo back into Hostify).
        ics.Should().NotContain($"ownerblock-{externalId.Value:N}@gaiaskyline");
        result.ETag.Should().NotBeNullOrEmpty();
    }
}
