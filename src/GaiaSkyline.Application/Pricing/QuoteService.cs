using GaiaSkyline.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Application.Pricing;

/// <summary>
/// Loads the pricing rules/fees/promo in force, stamps today's Lisbon date and the last-minute rule,
/// and delegates to the pure <see cref="IPricingCalculator"/>. The server always quotes here and
/// never trusts a client-supplied price.
/// </summary>
public sealed class QuoteService(
    IPricingReadStore readStore,
    IPricingCalculator calculator,
    TimeProvider clock,
    IOptions<BookingPricingOptions> options) : IQuoteService
{
    private readonly BookingPricingOptions _options = options.Value;

    public async Task<QuoteBreakdown> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rules = await readStore.GetPricingRulesAsync(request.CheckIn, request.CheckOut, cancellationToken);
        var fees = await readStore.GetFeesAsync(cancellationToken);
        var promo = string.IsNullOrWhiteSpace(request.PromoCode)
            ? null
            : await readStore.GetPromoCodeAsync(request.PromoCode, cancellationToken);

        // Per-date rates (always read from storage; never an external API during a request).
        var dailyRates = await readStore.GetDailyRatesAsync(request.CheckIn, request.CheckOut, cancellationToken);
        var dailyMap = dailyRates.ToDictionary(d => d.Date, d => new DailyRateValue(d.NightlyRate, d.MinNights, d.Source));

        var context = new PricingContext(
            rules,
            fees,
            promo,
            LisbonClock.Today(clock),
            _options.LastMinuteWindowDays,
            _options.LastMinuteMinNights,
            DailyRates: dailyMap,
            BaseNightlyRate: new Money(_options.BaseNightlyRateEur, "EUR"),
            BaseMinNights: _options.StandardMinNights);

        return calculator.Quote(request, context);
    }
}
