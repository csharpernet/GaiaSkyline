using System.Data;
using GaiaSkyline.Application.Availability;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>
/// Creates a booking with its nights held, proof against double-booking on two layers: a Serializable
/// transaction that re-checks availability before inserting, and the <see cref="BookingDateOccupancy"/>
/// date primary key, whose conflicting insert fails. Either way a loser sees a clean
/// <see cref="DatesUnavailableException"/>, never a raw SQL error.
/// </summary>
internal sealed class BookingCreationService(
    AppDbContext dbContext,
    IQuoteService quoteService,
    IPricingReadStore pricingReadStore,
    IBookingReferenceGenerator referenceGenerator,
    IAvailabilityService availabilityService,
    IIcsCacheInvalidator icsCacheInvalidator,
    GaiaSkyline.Application.Partners.IPartnerAttributionService partnerAttribution,
    TimeProvider clock) : IBookingCreationService
{
    private const int SqlUniqueViolation = 2627;
    private const int SqlUniqueIndexViolation = 2601;

    public async Task<Booking> CreateAsync(CreateBookingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Always re-quote on the server; the client price is never trusted.
        var quote = await quoteService.QuoteAsync(
            new QuoteRequest(command.CheckIn, command.CheckOut, command.Guests, command.PromoCode),
            cancellationToken);

        var promoId = await ResolveAppliedPromoAsync(command, quote, cancellationToken);
        var nights = EachNight(command.CheckIn, command.CheckOut);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var lastNight = command.CheckOut.AddDays(-1);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var booking = await strategy.ExecuteAsync(async () =>
        {
            // A transient-failure retry re-runs this delegate; start from a clean change tracker.
            dbContext.ChangeTracker.Clear();

            await using var transaction = await dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var occupied = await dbContext.BookingDateOccupancies.AsNoTracking()
                .AnyAsync(o => nights.Contains(o.Date), cancellationToken);
            if (occupied)
            {
                throw new DatesUnavailableException();
            }

            var externallyBlocked = await dbContext.ExternalCalendarBlocks.AsNoTracking()
                .AnyAsync(b => b.IsActive && b.StartDate <= lastNight && b.EndDate >= command.CheckIn, cancellationToken);
            if (externallyBlocked)
            {
                throw new DatesUnavailableException();
            }

            // Owner blocks use an exclusive end date.
            var ownerBlocked = await dbContext.OwnerBlocks.AsNoTracking()
                .AnyAsync(b => b.StartDate <= lastNight && b.EndDate > command.CheckIn, cancellationToken);
            if (ownerBlocked)
            {
                throw new DatesUnavailableException();
            }

            var newBooking = BuildBooking(command, quote, promoId, nowUtc);
            dbContext.Bookings.Add(newBooking);
            foreach (var night in nights)
            {
                dbContext.BookingDateOccupancies.Add(new BookingDateOccupancy(night, newBooking.Id));
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Lost the race on the occupancy primary key.
                throw new DatesUnavailableException();
            }

            await transaction.CommitAsync(cancellationToken);
            return newBooking;
        });

        availabilityService.Invalidate();
        icsCacheInvalidator.Invalidate();

        // Stage 8 Part A: record who referred this booking (code typed at checkout wins over the gs_ref
        // cookie, ADR 0019). Attribution must never fail a booking — it is best-effort bookkeeping.
        try
        {
            await partnerAttribution.AttributeBookingAsync(
                booking.Id.Value, command.PromoCode, command.ReferralCode, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Swallowed by design; the daily lifecycle job cannot recreate a missing attribution, but a
            // booking must never be lost to referral bookkeeping.
        }

        return booking;
    }

    private async Task<PromoCodeId?> ResolveAppliedPromoAsync(
        CreateBookingCommand command, QuoteBreakdown quote, CancellationToken cancellationToken)
    {
        if (quote.Discount.Kind != DiscountKind.Promo || string.IsNullOrWhiteSpace(command.PromoCode))
        {
            return null;
        }

        var promo = await pricingReadStore.GetPromoCodeAsync(command.PromoCode, cancellationToken);
        return promo?.Id;
    }

    private Booking BuildBooking(CreateBookingCommand command, QuoteBreakdown quote, PromoCodeId? promoId, DateTime nowUtc)
    {
        // A manual (owner-entered) total overrides the quote; the difference folds into the discount
        // line so the stored lines still add up. Callers validate the range (fees ≤ override ≤ total).
        var discount = quote.Discount.Amount;
        var total = quote.Total;
        if (command.TotalOverrideEur is { } agreed && agreed != total.Amount)
        {
            var gross = quote.NightlySubtotal.Amount + quote.CleaningFee.Amount + quote.TouristTax.Amount;
            discount = new Money(gross - agreed, total.Currency);
            total = new Money(agreed, total.Currency);
        }

        return new(
            BookingId.New(),
            referenceGenerator.Next(),
            command.CheckIn,
            command.CheckOut,
            command.Guests.Adults,
            command.Guests.Children,
            command.Guests.Infants,
            command.GuestName,
            command.GuestEmail,
            command.GuestPhone,
            command.GuestCountry,
            command.GuestLanguage,
            nightlyRateSnapshot: quote.Nightly.Count > 0 ? quote.Nightly[0].Rate : quote.NightlySubtotal,
            subtotal: quote.NightlySubtotal,
            discountAmount: discount,
            cleaningFee: quote.CleaningFee,
            touristTax: quote.TouristTax,
            total: total,
            createdAtUtc: nowUtc,
            promoCodeId: promoId,
            accountCreationRequested: command.AccountCreationRequested,
            arrivalEstimateLocal: command.ArrivalEstimateLocal,
            specialRequests: command.SpecialRequests);
    }

    private static List<DateOnly> EachNight(DateOnly checkIn, DateOnly checkOut)
    {
        var nights = new List<DateOnly>(checkOut.DayNumber - checkIn.DayNumber);
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            nights.Add(night);
        }

        return nights;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sql
        && sql.Number is SqlUniqueViolation or SqlUniqueIndexViolation;
}
