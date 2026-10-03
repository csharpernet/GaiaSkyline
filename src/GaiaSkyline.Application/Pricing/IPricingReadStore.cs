using GaiaSkyline.Domain.Pricing;

namespace GaiaSkyline.Application.Pricing;

/// <summary>Loads the pricing data a quote needs. Implemented in Infrastructure (EF Core, untracked).</summary>
public interface IPricingReadStore
{
    /// <summary>Pricing rules overlapping the stay [<paramref name="checkIn"/>, <paramref name="checkOut"/>).</summary>
    Task<IReadOnlyList<PricingRule>> GetPricingRulesAsync(DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken);

    Task<IReadOnlyList<Fee>> GetFeesAsync(CancellationToken cancellationToken);

    /// <summary>The promo code matching <paramref name="code"/> (case-insensitive), or null.</summary>
    Task<PromoCode?> GetPromoCodeAsync(string code, CancellationToken cancellationToken);

    Task<CancellationPolicy?> GetCancellationPolicyAsync(CancellationToken cancellationToken);
}
