using GaiaSkyline.Domain.Pricing;

namespace GaiaSkyline.Application.Pricing;

/// <summary>
/// Everything the pure <see cref="IPricingCalculator"/> needs beyond the request: the pricing rules
/// and fees in force, the already-resolved promo code (looked up by the caller), today's date
/// (Europe/Lisbon), and the last-minute minimum-nights rule.
/// </summary>
public sealed record PricingContext(
    IReadOnlyList<PricingRule> Rules,
    IReadOnlyList<Fee> Fees,
    PromoCode? Promo,
    DateOnly Today,
    int LastMinuteWindowDays,
    int LastMinuteMinNights);
