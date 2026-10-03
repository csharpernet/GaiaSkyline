using FluentAssertions;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Tests.Bookings;

public class BookingTests
{
    private static Money Eur(decimal amount) => new(amount, "EUR");

    private static Booking Build(
        int adults = 2,
        int children = 0,
        int infants = 0,
        decimal subtotal = 600m,
        decimal discount = 0m,
        decimal cleaning = 60m,
        decimal tax = 40m,
        decimal total = 700m,
        DateOnly? checkIn = null,
        DateOnly? checkOut = null)
    {
        var inDate = checkIn ?? new DateOnly(2026, 6, 1);
        var outDate = checkOut ?? new DateOnly(2026, 6, 6);
        return new Booking(
            BookingId.New(),
            referenceCode: "GS-8K3M",
            checkIn: inDate,
            checkOut: outDate,
            adults: adults,
            children: children,
            infants: infants,
            guestName: "Ana Guest",
            guestEmail: "ana@example.com",
            guestPhone: "+351 912 345 678",
            guestCountry: "Portugal",
            guestLanguage: "pt-PT",
            nightlyRateSnapshot: Eur(120m),
            subtotal: Eur(subtotal),
            discountAmount: Eur(discount),
            cleaningFee: Eur(cleaning),
            touristTax: Eur(tax),
            total: Eur(total),
            createdAtUtc: new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void A_new_booking_starts_awaiting_payment_with_nights_computed()
    {
        var booking = Build(checkIn: new DateOnly(2026, 6, 1), checkOut: new DateOnly(2026, 6, 6));

        booking.Status.Should().Be(BookingStatus.AwaitingPayment);
        booking.Nights.Should().Be(5);
        booking.ReferenceCode.Should().Be("GS-8K3M");
    }

    [Fact]
    public void Check_out_must_be_after_check_in()
    {
        var act = () => Build(checkIn: new DateOnly(2026, 6, 6), checkOut: new DateOnly(2026, 6, 6));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void At_least_one_adult_is_required()
    {
        var act = () => Build(adults: 0, children: 2);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Occupancy_is_capped_at_six_excluding_infants()
    {
        var tooMany = () => Build(adults: 4, children: 3);
        tooMany.Should().Throw<ArgumentOutOfRangeException>();

        var sixPlusInfant = () => Build(adults: 6, children: 0, infants: 1);
        sixPlusInfant.Should().NotThrow();
    }

    [Fact]
    public void At_most_one_infant_is_allowed()
    {
        var act = () => Build(adults: 2, infants: 2);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Total_must_equal_the_sum_of_its_components()
    {
        var act = () => Build(subtotal: 600m, discount: 0m, cleaning: 60m, tax: 40m, total: 999m);
        act.Should().Throw<BookingTotalMismatchException>();
    }

    [Fact]
    public void Total_accounts_for_discount()
    {
        // 600 - 60 + 60 + 40 = 640
        var act = () => Build(subtotal: 600m, discount: 60m, cleaning: 60m, tax: 40m, total: 640m);
        act.Should().NotThrow();
    }

    [Fact]
    public void Confirming_payment_moves_to_confirmed_and_records_details()
    {
        var booking = Build();

        booking.ConfirmPayment("card", new DateTime(2026, 5, 1, 12, 5, 0, DateTimeKind.Utc));

        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.PaymentMethodType.Should().Be("card");
        booking.ConfirmedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Confirming_an_already_confirmed_booking_is_rejected()
    {
        var booking = Build();
        booking.ConfirmPayment("card", DateTime.UtcNow);

        var act = () => booking.ConfirmPayment("card", DateTime.UtcNow);
        act.Should().Throw<InvalidBookingStatusTransitionException>();
    }

    [Fact]
    public void Multibanco_voucher_details_are_recorded()
    {
        var booking = Build();
        var expiry = new DateTime(2026, 5, 11, 23, 59, 0, DateTimeKind.Utc);

        booking.SetMultibancoVoucher("12345", "987654321", expiry);

        booking.MultibancoEntity.Should().Be("12345");
        booking.MultibancoReference.Should().Be("987654321");
        booking.PaymentExpiresAtUtc.Should().Be(expiry);
    }
}
