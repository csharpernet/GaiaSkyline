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

        var context = new PricingContext(
            rules,
            fees,
            promo,
            LisbonClock.Today(clock),
            _options.LastMinuteWindowDays,
            _options.LastMinuteMinNights);

        return calculator.Quote(request, context);
    }
}
