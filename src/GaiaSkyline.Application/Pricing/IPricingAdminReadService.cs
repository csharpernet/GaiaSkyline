namespace GaiaSkyline.Application.Pricing;

/// <summary>
/// One calendar day in the rates grid: the effective nightly price, the minimum stay in force, where
/// the price comes from ("Manual"/"PriceLabs"/"Hostify" per-date rate, "Season" rule, or "Base"),
/// whether a per-date override row exists, and the owner lock.
/// </summary>
public sealed record RateDayDto(
    DateOnly Date,
    decimal? PriceEur,
    int MinNights,
    string Source,
    bool HasOverride,
    bool Locked);

public sealed record RatesMonthDto(DateOnly Month, IReadOnlyList<RateDayDto> Days);

/// <summary>A dry-run CSV row joined with the value currently in force (for the old-vs-new preview).</summary>
public sealed record CsvPreviewRowDto(
    string Status,
    DateOnly? Date,
    decimal? NewPriceEur,
    int? NewMinNights,
    decimal? CurrentPriceEur,
    int? CurrentMinNights,
    string CurrentSource,
    bool Changes);

/// <summary>Owner-only reads for the prices admin (Stage 7 §8).</summary>
public interface IPricingAdminReadService
{
    /// <summary>The rates grid: <paramref name="months"/> calendar months starting at <paramref name="firstMonth"/>'s month.</summary>
    Task<IReadOnlyList<RatesMonthDto>> GetGridAsync(DateOnly firstMonth, int months, CancellationToken cancellationToken);

    Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(CancellationToken cancellationToken);

    Task<FeesDto> GetFeesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Domain.Pricing.CancellationTier>> GetPolicyTiersAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PromoDto>> GetPromosAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RejectionDto>> GetOpenRejectionsAsync(CancellationToken cancellationToken);

    /// <summary>The dry-run preview rows joined with the current effective values, old vs new.</summary>
    Task<IReadOnlyList<CsvPreviewRowDto>> PreviewCsvAsync(string csv, CancellationToken cancellationToken);

    /// <summary>The per-date override rows as CSV (<c>date,price,min_nights</c>) for download.</summary>
    Task<string> ExportRatesCsvAsync(CancellationToken cancellationToken);
}
