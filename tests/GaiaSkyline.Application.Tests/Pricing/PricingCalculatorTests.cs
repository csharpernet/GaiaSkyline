using FluentAssertions;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Application.Tests.Pricing;

public class PricingCalculatorTests
{
    private static readonly DateOnly FarPastToday = new(2026, 1, 1);
    private readonly PricingCalculator _calculator = new();

    private static Money Eur(decimal amount) => new(amount, "EUR");

    private static PricingRule Rule(
        DateOnly start, DateOnly end, decimal rate,
        int minNights = 3, int weekly = 0, int monthly = 0) =>
        new(PricingRuleId.New(), start, end, Eur(rate), minNights, weekly, monthly);

    private static PricingRule YearRule(decimal rate, int minNights = 3, int weekly = 0, int monthly = 0) =>
        Rule(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), rate, minNights, weekly, monthly);

    private static Fee Cleaning(decimal amount) =>
        new(FeeId.New(), FeeType.Cleaning, Eur(amount), isPerNight: false, isPerGuest: false);

    private static Fee TouristTax(decimal amount, int? maxNights = null) =>
        new(FeeId.New(), FeeType.TouristTax, Eur(amount), isPerNight: true, isPerGuest: true, maxNights: maxNights, minAgeExempt: 13);

    private static PricingContext Ctx(
        IEnumerable<PricingRule> rules,
        IEnumerable<Fee>? fees = null,
        PromoCode? promo = null,
        DateOnly? today = null,
        int window = 7,
        int lastMin = 1) =>
        new(rules.ToList(), (fees ?? []).ToList(), promo, today ?? FarPastToday, window, lastMin);

    private static QuoteRequest Request(
        DateOnly checkIn, DateOnly checkOut,
        int adults = 2, int children = 0, int infants = 0, string? promo = null) =>
        new(checkIn, checkOut, new GuestParty(adults, children, infants), promo);

    [Fact]
    public void Prices_a_simple_stay_with_no_discounts_or_fees()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 4));

        var quote = _calculator.Quote(request, Ctx([YearRule(100m)]));

        quote.Nights.Should().Be(3);
        quote.NightlySubtotal.Amount.Should().Be(300m);
        quote.Discount.Kind.Should().Be(DiscountKind.None);
        quote.Total.Amount.Should().Be(300m);
    }

    [Fact]
    public void Adds_the_cleaning_fee_once_per_stay()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 4));

        var quote = _calculator.Quote(request, Ctx([YearRule(100m)], fees: [Cleaning(60m)]));

        quote.CleaningFee.Amount.Should().Be(60m);
        quote.Total.Amount.Should().Be(360m);
    }

    [Fact]
    public void Prices_each_night_from_the_rule_covering_it_across_seasons()
    {
        var low = Rule(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), 100m);
        var high = Rule(new DateOnly(2026, 6, 3), new DateOnly(2026, 6, 30), 150m);
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 4));

        var quote = _calculator.Quote(request, Ctx([low, high]));

        // 6/1 + 6/2 at 100, 6/3 at 150
        quote.NightlySubtotal.Amount.Should().Be(350m);
        quote.Nightly.Should().HaveCount(3);
    }

    [Fact]
    public void Applies_the_weekly_discount_at_seven_nights()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8)); // 7 nights

        var quote = _calculator.Quote(request, Ctx([YearRule(100m, weekly: 10)]));

        quote.Discount.Kind.Should().Be(DiscountKind.Weekly);
        quote.Discount.Percent.Should().Be(10);
        quote.Discount.Amount.Amount.Should().Be(70m);
        quote.Total.Amount.Should().Be(630m);
    }

    [Fact]
    public void Applies_the_monthly_discount_at_twenty_eight_nights()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 29)); // 28 nights

        var quote = _calculator.Quote(request, Ctx([YearRule(100m, weekly: 10, monthly: 20)]));

        quote.Discount.Kind.Should().Be(DiscountKind.Monthly);
        quote.Discount.Amount.Amount.Should().Be(560m);
    }

    [Fact]
    public void No_length_of_stay_discount_under_seven_nights()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5)); // 4 nights

        var quote = _calculator.Quote(request, Ctx([YearRule(100m, weekly: 10)]));

        quote.Discount.Kind.Should().Be(DiscountKind.None);
    }

    [Fact]
    public void Promo_wins_when_larger_than_the_length_of_stay_discount()
    {
        var promo = new PromoCode(PromoCodeId.New(), "SAVE20", 20, isActive: true);
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8), promo: "save20"); // 7 nights

        var quote = _calculator.Quote(request, Ctx([YearRule(100m, weekly: 10)], promo: promo));

        quote.Discount.Kind.Should().Be(DiscountKind.Promo);
        quote.Discount.Percent.Should().Be(20);
        quote.Discount.Amount.Amount.Should().Be(140m);
        quote.PromoRequestedButInvalid.Should().BeFalse();
    }

    [Fact]
    public void Length_of_stay_discount_wins_when_larger_than_the_promo()
    {
        var promo = new PromoCode(PromoCodeId.New(), "SAVE10", 10, isActive: true);
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8), promo: "SAVE10"); // 7 nights

        var quote = _calculator.Quote(request, Ctx([YearRule(100m, weekly: 25)], promo: promo));

        quote.Discount.Kind.Should().Be(DiscountKind.Weekly);
        quote.Discount.Percent.Should().Be(25);
    }

    [Fact]
    public void Discounts_never_stack()
    {
        var promo = new PromoCode(PromoCodeId.New(), "SAVE20", 20, isActive: true);
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8), promo: "SAVE20");

        var quote = _calculator.Quote(request, Ctx([YearRule(100m, weekly: 10)], promo: promo));

        // Only the 20% promo, not 30%.
        quote.Discount.Amount.Amount.Should().Be(140m);
    }

    [Fact]
    public void An_unknown_promo_code_is_flagged_and_ignored()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), promo: "BOGUS");

        var quote = _calculator.Quote(request, Ctx([YearRule(100m)], promo: null));

        quote.PromoRequestedButInvalid.Should().BeTrue();
        quote.Discount.Kind.Should().Be(DiscountKind.None);
    }

    [Fact]
    public void An_inactive_promo_code_does_not_apply()
    {
        var promo = new PromoCode(PromoCodeId.New(), "OFF", 50, isActive: false);
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 5), promo: "OFF");

        var quote = _calculator.Quote(request, Ctx([YearRule(100m)], promo: promo));

        quote.PromoRequestedButInvalid.Should().BeTrue();
        quote.Discount.Kind.Should().Be(DiscountKind.None);
    }

    [Fact]
    public void Tourist_tax_is_per_adult_per_night_capped_and_exempts_minors()
    {
        // 10 nights, €2/adult/night capped at 7 nights; 2 adults, children + infant exempt.
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 11), adults: 2, children: 2, infants: 1);

        var quote = _calculator.Quote(request, Ctx([YearRule(100m)], fees: [TouristTax(2m, maxNights: 7)]));

        quote.TouristTax.Amount.Should().Be(28m); // 2 adults * 2 EUR * 7 capped nights
    }

    [Fact]
    public void Tourist_tax_without_a_cap_charges_every_night()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 4), adults: 2);

        var quote = _calculator.Quote(request, Ctx([YearRule(100m)], fees: [TouristTax(2m)]));

        quote.TouristTax.Amount.Should().Be(12m); // 2 adults * 2 EUR * 3 nights
    }

    [Fact]
    public void No_tourist_tax_fee_means_no_tax()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 4));

        var quote = _calculator.Quote(request, Ctx([YearRule(100m)]));

        quote.TouristTax.Amount.Should().Be(0m);
    }

    [Fact]
    public void Percentage_discounts_are_rounded_to_the_cent()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8)); // 7 nights

        var quote = _calculator.Quote(request, Ctx([YearRule(100.05m, weekly: 10)]));

        // 700.35 * 10% = 70.035 -> 70.04 (away from zero)
        quote.NightlySubtotal.Amount.Should().Be(700.35m);
        quote.Discount.Amount.Amount.Should().Be(70.04m);
    }

    [Fact]
    public void Total_always_equals_subtotal_minus_discount_plus_fees_and_tax()
    {
        var promo = new PromoCode(PromoCodeId.New(), "SAVE15", 15, isActive: true);
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 8), adults: 3, promo: "SAVE15");

        var quote = _calculator.Quote(
            request,
            Ctx([YearRule(120m, weekly: 10)], fees: [Cleaning(60m), TouristTax(2m, maxNights: 7)], promo: promo));

        var expected = quote.NightlySubtotal.Amount - quote.Discount.Amount.Amount
            + quote.CleaningFee.Amount + quote.TouristTax.Amount;
        quote.Total.Amount.Should().Be(expected);
    }

    [Fact]
    public void A_stay_below_the_standard_minimum_is_rejected()
    {
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3)); // 2 nights

        var act = () => _calculator.Quote(request, Ctx([YearRule(100m, minNights: 3)]));

        act.Should().Throw<BelowMinimumNightsException>().Which.MinimumNights.Should().Be(3);
    }

    [Fact]
    public void A_single_night_is_allowed_within_the_last_minute_window()
    {
        var today = new DateOnly(2026, 6, 1);
        var request = Request(new DateOnly(2026, 6, 6), new DateOnly(2026, 6, 7)); // 1 night, 5 days out

        var quote = _calculator.Quote(
            request,
            Ctx([YearRule(100m, minNights: 3)], today: today, window: 7, lastMin: 1));

        quote.Nights.Should().Be(1);
        quote.EffectiveMinNights.Should().Be(1);
        quote.Total.Amount.Should().Be(100m);
    }

    [Fact]
    public void The_last_minute_window_boundary_is_inclusive()
    {
        var today = new DateOnly(2026, 6, 1);
        var request = Request(new DateOnly(2026, 6, 8), new DateOnly(2026, 6, 9)); // exactly 7 days out

        var quote = _calculator.Quote(
            request,
            Ctx([YearRule(100m, minNights: 3)], today: today, window: 7, lastMin: 1));

        quote.EffectiveMinNights.Should().Be(1);
    }

    [Fact]
    public void Outside_the_last_minute_window_the_standard_minimum_applies()
    {
        var today = new DateOnly(2026, 6, 1);
        var request = Request(new DateOnly(2026, 6, 9), new DateOnly(2026, 6, 10)); // 8 days out, 1 night

        var act = () => _calculator.Quote(
            request,
            Ctx([YearRule(100m, minNights: 3)], today: today, window: 7, lastMin: 1));

        act.Should().Throw<BelowMinimumNightsException>();
    }

    [Fact]
    public void A_night_with_no_rule_is_an_error()
    {
        var onlyFirstTwo = Rule(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2), 100m);
        var request = Request(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 4)); // needs 6/3 too

        var act = () => _calculator.Quote(request, Ctx([onlyFirstTwo]));

        act.Should().Throw<NoPriceForDateException>().Which.Date.Should().Be(new DateOnly(2026, 6, 3));
    }

    [Fact]
    public void Check_out_must_be_after_check_in()
    {
        var request = Request(new DateOnly(2026, 6, 4), new DateOnly(2026, 6, 4));

        var act = () => _calculator.Quote(request, Ctx([YearRule(100m)]));

        act.Should().Throw<ArgumentException>();
    }
}
