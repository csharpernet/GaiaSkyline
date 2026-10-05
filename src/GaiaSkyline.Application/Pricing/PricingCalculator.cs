using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Application.Pricing;

/// <summary>
/// Default pricing implementation. See ADR 0009 for the rules: per-night rates (seasons priced per
/// night), a single discount that is the larger of the length-of-stay or promo discount (never
/// stacked, applied to the nightly subtotal only), tourist tax per adult per night capped at the
/// fee's max nights, and a last-minute minimum-nights exception.
/// </summary>
public sealed class PricingCalculator : IPricingCalculator
{
    private const string Currency = "EUR";
    private const int WeeklyThreshold = 7;
    private const int MonthlyThreshold = 28;

    public QuoteBreakdown Quote(QuoteRequest request, PricingContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);

        if (request.CheckOut <= request.CheckIn)
        {
            throw new ArgumentException("Check-out must be after check-in.", nameof(request));
        }

        var nights = request.CheckOut.DayNumber - request.CheckIn.DayNumber;

        // Per-night rates — resolution per night is daily rate → season rule → base rate.
        var nightly = new List<NightlyCharge>(nights);
        var nightlySubtotal = Money.Zero(Currency);
        for (var night = request.CheckIn; night < request.CheckOut; night = night.AddDays(1))
        {
            var (rate, source) = ResolveNightlyRate(night, context);
            nightly.Add(new NightlyCharge(night, rate, source));
            nightlySubtotal += rate;
        }

        var checkInRule = context.Rules.FirstOrDefault(r => r.Covers(request.CheckIn));
        EnsureMeetsMinimumNights(request, context, checkInRule, nights, out var effectiveMinNights);

        var (discount, promoInvalid) = ResolveDiscount(request, context, checkInRule, nights, nightlySubtotal);

        var cleaningFee = SumFees(context.Fees, FeeType.Cleaning);
        var touristTax = CalculateTouristTax(context.Fees, request.Guests, nights);

        var total = nightlySubtotal - discount.Amount + cleaningFee + touristTax;

        return new QuoteBreakdown(
            nights,
            nightly,
            nightlySubtotal,
            discount,
            cleaningFee,
            touristTax,
            total,
            effectiveMinNights,
            promoInvalid);
    }

    private static (Money Rate, string Source) ResolveNightlyRate(DateOnly night, PricingContext context)
    {
        if (context.DailyRates is not null && context.DailyRates.TryGetValue(night, out var daily))
        {
            return (daily.NightlyRate, daily.Source.ToString());
        }

        var rule = context.Rules.FirstOrDefault(r => r.Covers(night));
        if (rule is not null)
        {
            return (rule.NightlyRate, "Season");
        }

        return (context.BaseNightlyRate ?? throw new NoPriceForDateException(night), "Base");
    }

    private static void EnsureMeetsMinimumNights(
        QuoteRequest request,
        PricingContext context,
        PricingRule? checkInRule,
        int nights,
        out int effectiveMinNights)
    {
        var leadDays = request.CheckIn.DayNumber - context.Today.DayNumber;
        var lastMinute = leadDays <= context.LastMinuteWindowDays;

        // Minimum-nights resolution: daily rate (check-in) → season rule → base → 1.
        int? dailyMinNights = null;
        if (context.DailyRates is not null && context.DailyRates.TryGetValue(request.CheckIn, out var daily))
        {
            dailyMinNights = daily.MinNights;
        }

        var standardMinNights = dailyMinNights ?? checkInRule?.MinNights ?? context.BaseMinNights ?? 1;
        effectiveMinNights = lastMinute ? context.LastMinuteMinNights : standardMinNights;

        if (nights < effectiveMinNights)
        {
            throw new BelowMinimumNightsException(effectiveMinNights);
        }
    }

    private static (DiscountLine Discount, bool PromoInvalid) ResolveDiscount(
        QuoteRequest request,
        PricingContext context,
        PricingRule? checkInRule,
        int nights,
        Money nightlySubtotal)
    {
        var (lengthOfStayKind, lengthOfStayPct) = LengthOfStayDiscount(checkInRule, nights);

        var promoRequested = !string.IsNullOrWhiteSpace(request.PromoCode);
        var promoPct = 0;
        var promoInvalid = false;
        if (promoRequested)
        {
            var normalized = request.PromoCode!.Trim().ToUpperInvariant();
            var promo = context.Promo;
            if (promo is not null && promo.Code == normalized && promo.IsRedeemableOn(context.Today))
            {
                promoPct = promo.DiscountPct;
            }
            else
            {
                promoInvalid = true;
            }
        }

        // Never stack: apply whichever single discount is larger.
        DiscountKind kind;
        int percent;
        if (promoPct > lengthOfStayPct)
        {
            kind = DiscountKind.Promo;
            percent = promoPct;
        }
        else if (lengthOfStayPct > 0)
        {
            kind = lengthOfStayKind;
            percent = lengthOfStayPct;
        }
        else
        {
            return (new DiscountLine(DiscountKind.None, 0, Money.Zero(Currency)), promoInvalid);
        }

        var raw = nightlySubtotal.Amount * percent / 100m;
        var amount = new Money(Math.Round(raw, 2, MidpointRounding.AwayFromZero), Currency);
        return (new DiscountLine(kind, percent, amount), promoInvalid);
    }

    private static (DiscountKind Kind, int Percent) LengthOfStayDiscount(PricingRule? rule, int nights)
    {
        if (rule is null)
        {
            return (DiscountKind.None, 0);
        }

        if (nights >= MonthlyThreshold && rule.MonthlyDiscountPct > 0)
        {
            return (DiscountKind.Monthly, rule.MonthlyDiscountPct);
        }

        if (nights >= WeeklyThreshold && rule.WeeklyDiscountPct > 0)
        {
            return (DiscountKind.Weekly, rule.WeeklyDiscountPct);
        }

        return (DiscountKind.None, 0);
    }

    private static Money SumFees(IReadOnlyList<Fee> fees, FeeType type)
    {
        var total = Money.Zero(Currency);
        foreach (var fee in fees.Where(f => f.Type == type))
        {
            total += fee.Amount;
        }

        return total;
    }

    private static Money CalculateTouristTax(IReadOnlyList<Fee> fees, GuestParty guests, int nights)
    {
        var total = Money.Zero(Currency);
        foreach (var fee in fees.Where(f => f.Type == FeeType.TouristTax))
        {
            // Per adult (children/infants exempt); capped at the fee's max nights.
            var payers = fee.IsPerGuest ? guests.Adults : 1;
            var chargedNights = fee.IsPerNight ? Math.Min(nights, fee.MaxNights ?? nights) : 1;
            total += fee.Amount * (payers * chargedNights);
        }

        return total;
    }
}
