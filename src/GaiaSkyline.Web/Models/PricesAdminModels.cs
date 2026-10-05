using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Pricing;

namespace GaiaSkyline.Web.Models;

/// <summary>The admin quote-preview form (dates + party + promo), kept populated across posts.</summary>
public sealed class QuotePreviewForm
{
    public DateOnly? CheckIn { get; set; }

    public DateOnly? CheckOut { get; set; }

    public int Adults { get; set; } = 2;

    public int Children { get; set; }

    public int Infants { get; set; }

    public string? Promo { get; set; }
}

/// <summary>/admin/prices: the 12-month rates grid plus the CSV, quote-preview and rejection panels.</summary>
public sealed record PricesAdminViewModel(
    IReadOnlyList<RatesMonthDto> Grid,
    IReadOnlyList<RejectionDto> Rejections,
    IReadOnlyList<CsvPreviewRowDto>? CsvPreview,
    string? CsvRaw,
    QuoteBreakdown? Quote,
    string? QuoteError,
    QuotePreviewForm QuoteForm);

/// <summary>/admin/prices/setup: seasons, fees, cancellation tiers and promo codes.</summary>
public sealed record PricesSetupViewModel(
    IReadOnlyList<SeasonDto> Seasons,
    FeesDto Fees,
    IReadOnlyList<CancellationTier> Tiers,
    IReadOnlyList<PromoDto> Promos);
