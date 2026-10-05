using GaiaSkyline.Domain.Pricing;

namespace GaiaSkyline.Application.Pricing;

/// <summary>Outcome of a pricing admin write; <see cref="Error"/> is user-facing when it fails.</summary>
public sealed record PricingAdminResult(bool Ok, string? Error)
{
    public static PricingAdminResult Success { get; } = new(true, null);

    public static PricingAdminResult Fail(string error) => new(false, error);
}

public sealed record SeasonDto(
    Guid Id,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal NightlyRateEur,
    int MinNights,
    int WeeklyDiscountPct,
    int MonthlyDiscountPct);

public sealed record SeasonWriteModel(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal NightlyRateEur,
    int MinNights,
    int WeeklyDiscountPct,
    int MonthlyDiscountPct);

/// <summary>Cleaning is a flat per-stay amount; the tourist tax is per adult per night capped at MaxNights (null amount = no tax).</summary>
public sealed record FeesDto(decimal CleaningFeeEur, decimal? TouristTaxPerAdultPerNightEur, int? TouristTaxMaxNights);

public sealed record PromoDto(
    Guid Id,
    string Code,
    int DiscountPct,
    bool IsActive,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil);

public sealed record RejectionDto(
    Guid Id,
    DateOnly Date,
    decimal OfferedPriceEur,
    int? OfferedMinNights,
    string Provider,
    string Reason,
    DateTime DetectedAtUtc);

/// <summary>
/// Owner-only pricing catalogue writes (Stage 7 §8): seasons (no overlaps), fees, cancellation-policy
/// tiers, promo codes, and the rate-sync rejection review. Every mutation bumps the content revision so
/// cached public pages refresh; quotes always read live.
/// </summary>
public interface IPricingAdminService
{
    Task<PricingAdminResult> CreateSeasonAsync(SeasonWriteModel season, CancellationToken cancellationToken);

    Task<PricingAdminResult> UpdateSeasonAsync(Guid id, SeasonWriteModel season, CancellationToken cancellationToken);

    Task<PricingAdminResult> DeleteSeasonAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Updates the cleaning fee and the tourist-tax rule (a null tax amount removes the tax).</summary>
    Task<PricingAdminResult> UpdateFeesAsync(FeesDto fees, CancellationToken cancellationToken);

    /// <summary>Replaces the cancellation-policy tiers (ordered by days-before-check-in internally).</summary>
    Task<PricingAdminResult> ReplacePolicyTiersAsync(IReadOnlyList<CancellationTier> tiers, CancellationToken cancellationToken);

    Task<PricingAdminResult> CreatePromoAsync(
        string code, int discountPct, bool isActive, DateOnly? validFrom, DateOnly? validUntil, CancellationToken cancellationToken);

    Task<PricingAdminResult> UpdatePromoAsync(
        Guid id, int discountPct, bool isActive, DateOnly? validFrom, DateOnly? validUntil, CancellationToken cancellationToken);

    Task<PricingAdminResult> DeletePromoAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Accepts a rejected provider price as a manual rate for that date.</summary>
    Task<PricingAdminResult> AcceptRejectionAsync(Guid id, string actor, CancellationToken cancellationToken);

    Task<PricingAdminResult> DismissRejectionAsync(Guid id, CancellationToken cancellationToken);
}
