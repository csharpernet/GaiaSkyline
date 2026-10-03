namespace GaiaSkyline.Application.Pricing;

/// <summary>
/// Pure, deterministic pricing. Given a request and the rules/fees/promo in force, it returns the
/// full line-item breakdown — or throws a <see cref="PricingException"/> for an unpriceable or
/// too-short stay. No I/O: the caller loads the <see cref="PricingContext"/> from storage.
/// </summary>
public interface IPricingCalculator
{
    QuoteBreakdown Quote(QuoteRequest request, PricingContext context);
}
