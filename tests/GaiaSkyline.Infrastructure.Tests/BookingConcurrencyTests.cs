using FluentAssertions;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using GaiaSkyline.Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class BookingConcurrencyTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>, IDisposable
{
    private readonly LocalDbFixture _fixture = fixture;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly AvailabilityCacheState _cacheState = new();

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task Two_concurrent_bookings_for_the_same_dates_only_one_wins()
    {
        await EnsurePricingAsync();
        var checkIn = new DateOnly(2027, 3, 1);
        var checkOut = new DateOnly(2027, 3, 4);

        async Task<bool> TryCreateAsync(string email)
        {
            await using var context = _fixture.CreateContext();
            var (creation, _, _) = BuildServices(context);
            try
            {
                await creation.CreateAsync(Command(checkIn, checkOut, email), CancellationToken.None);
                return true;
            }
            catch (DatesUnavailableException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(TryCreateAsync("a@example.com"), TryCreateAsync("b@example.com"));

        results.Count(won => won).Should().Be(1, "exactly one booking should win the race");
        results.Count(won => !won).Should().Be(1, "the loser should get a clean DatesUnavailable");

        await using var verify = _fixture.CreateContext();
        (await verify.Bookings.CountAsync(b => b.CheckIn == checkIn)).Should().Be(1);
        (await verify.BookingDateOccupancies.CountAsync(o => o.Date >= checkIn && o.Date < checkOut)).Should().Be(3);
    }

    [Fact]
    public async Task Cancelling_a_booking_releases_its_dates()
    {
        await EnsurePricingAsync();
        var checkIn = new DateOnly(2027, 4, 1);
        var checkOut = new DateOnly(2027, 4, 4);

        BookingId id;
        await using (var context = _fixture.CreateContext())
        {
            var (creation, _, _) = BuildServices(context);
            var booking = await creation.CreateAsync(Command(checkIn, checkOut, "c@example.com"), CancellationToken.None);
            id = booking.Id;
        }

        await using (var context = _fixture.CreateContext())
        {
            (await context.BookingDateOccupancies.CountAsync(o => o.BookingId == id)).Should().Be(3);
        }

        await using (var context = _fixture.CreateContext())
        {
            var (_, _, lifecycle) = BuildServices(context);
            await lifecycle.CancelAndReleaseAsync(id, "guest cancelled", CancellationToken.None);
        }

        await using (var context = _fixture.CreateContext())
        {
            (await context.BookingDateOccupancies.CountAsync(o => o.BookingId == id)).Should().Be(0);
            var booking = await context.Bookings.FirstAsync(b => b.Id == id);
            booking.Status.Should().Be(BookingStatus.Cancelled);
        }

        // The freed dates can be booked again.
        await using (var context = _fixture.CreateContext())
        {
            var (creation, _, _) = BuildServices(context);
            var act = async () => await creation.CreateAsync(Command(checkIn, checkOut, "d@example.com"), CancellationToken.None);
            await act.Should().NotThrowAsync();
        }
    }

    [Fact]
    public async Task Availability_reports_booked_nights_as_blocked()
    {
        await EnsurePricingAsync();
        var checkIn = new DateOnly(2027, 5, 1);
        var checkOut = new DateOnly(2027, 5, 4);

        await using (var context = _fixture.CreateContext())
        {
            var (creation, _, _) = BuildServices(context);
            await creation.CreateAsync(Command(checkIn, checkOut, "e@example.com"), CancellationToken.None);
        }

        await using (var context = _fixture.CreateContext())
        {
            var (_, availability, _) = BuildServices(context);
            var blocked = await availability.GetBlockedDatesAsync(
                new DateOnly(2027, 5, 1), new DateOnly(2027, 6, 1), CancellationToken.None);

            blocked.Should().Contain(new DateOnly(2027, 5, 1));
            blocked.Should().Contain(new DateOnly(2027, 5, 2));
            blocked.Should().Contain(new DateOnly(2027, 5, 3));
            blocked.Should().NotContain(new DateOnly(2027, 5, 4)); // the check-out day is free
        }
    }

    private (IBookingCreationService Creation, IAvailabilityService Availability, IBookingLifecycleService Lifecycle)
        BuildServices(AppDbContext context)
    {
        var calculator = new PricingCalculator();
        var readStore = new PricingReadStore(context);
        var options = Options.Create(new BookingPricingOptions
        {
            BaseNightlyRateEur = 100m,
            CleaningFeeEur = 60m,
            StandardMinNights = 3,
            LastMinuteWindowDays = 7,
            LastMinuteMinNights = 1,
        });
        var quotes = new QuoteService(readStore, calculator, TimeProvider.System, options);
        var availability = new AvailabilityService(context, _cache, _cacheState);
        var creation = new BookingCreationService(
            context, quotes, readStore, new BookingReferenceGenerator(), availability, TimeProvider.System);
        var lifecycle = new BookingLifecycleService(context, availability, TimeProvider.System);
        return (creation, availability, lifecycle);
    }

    private static CreateBookingCommand Command(DateOnly checkIn, DateOnly checkOut, string email) =>
        new(
            checkIn,
            checkOut,
            new GuestParty(2, 0, 0),
            GuestName: "Test Guest",
            GuestEmail: email,
            GuestPhone: "+351 912 345 678",
            GuestCountry: "Portugal",
            GuestLanguage: "pt-PT");

    private async Task EnsurePricingAsync()
    {
        await using var context = _fixture.CreateContext();
        if (!await context.PricingRules.AnyAsync())
        {
            context.PricingRules.Add(new PricingRule(
                PricingRuleId.New(),
                new DateOnly(2026, 1, 1),
                new DateOnly(2035, 12, 31),
                new Money(100m, "EUR"),
                minNights: 3,
                weeklyDiscountPct: 0,
                monthlyDiscountPct: 0));
        }

        if (!await context.Fees.AnyAsync())
        {
            context.Fees.Add(new Fee(
                FeeId.New(), FeeType.Cleaning, new Money(60m, "EUR"), isPerNight: false, isPerGuest: false));
        }

        await context.SaveChangesAsync();
    }
}
