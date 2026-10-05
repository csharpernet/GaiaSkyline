using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Application.Pricing;

/// <summary>Which discount, if any, was applied. Length-of-stay and promo never stack (ADR 0009).</summary>
public enum DiscountKind
{
    None,
    Weekly,
    Monthly,
    Promo,
}

/// <summary>
/// The nightly rate applied to a single night (handles stays that span pricing seasons).
/// <see cref="Source"/> names where the price came from — "Manual"/"PriceLabs"/"Hostify" (per-date
/// rate), "Season" (pricing rule) or "Base" (fallback) — surfaced in the admin quote preview.
/// </summary>
public sealed record NightlyCharge(DateOnly Date, Money Rate, string Source = "Base");

/// <summary>The single applied discount line (<see cref="DiscountKind.None"/> with a zero amount if none).</summary>
public sealed record DiscountLine(DiscountKind Kind, int Percent, Money Amount);

/// <summary>
/// A full line-item price breakdown for a stay. The same shape backs both the <c>/api/quote</c>
/// response and server-side booking creation — the server always re-quotes and never trusts a
/// client-supplied total.
/// </summary>
public sealed record QuoteBreakdown(
    int Nights,
    IReadOnlyList<NightlyCharge> Nightly,
    Money NightlySubtotal,
    DiscountLine Discount,
    Money CleaningFee,
    Money TouristTax,
    Money Total,
    int EffectiveMinNights,
    bool PromoRequestedButInvalid);
