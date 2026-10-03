using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Application.Pricing;

/// <summary>A resolved per-date rate for the calculator (nightly rate + optional minimum nights).</summary>
public sealed record DailyRateValue(Money NightlyRate, int? MinNights);

/// <summary>
/// Everything the pure <see cref="IPricingCalculator"/> needs beyond the request: the pricing rules
/// and fees in force, the already-resolved promo code (looked up by the caller), today's date
/// (Europe/Lisbon), and the last-minute minimum-nights rule. Per-date <see cref="DailyRates"/> and the
/// <see cref="BaseNightlyRate"/>/<see cref="BaseMinNights"/> fallbacks are optional (Stage 5 item 2):
/// price resolution per night is daily rate → season rule → base.
/// </summary>
public sealed record PricingContext(
    IReadOnlyList<PricingRule> Rules,
    IReadOnlyList<Fee> Fees,
    PromoCode? Promo,
    DateOnly Today,
    int LastMinuteWindowDays,
    int LastMinuteMinNights,
    IReadOnlyDictionary<DateOnly, DailyRateValue>? DailyRates = null,
    Money? BaseNightlyRate = null,
    int? BaseMinNights = null);
