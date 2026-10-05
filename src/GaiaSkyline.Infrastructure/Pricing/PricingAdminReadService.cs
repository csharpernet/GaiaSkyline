using System.Globalization;
using System.Text;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Pricing;

/// <summary>
/// Reads for the prices admin (Stage 7 §8): the rates grid resolves each night the same way the quote
/// calculator does (per-date rate → season rule → base), the CSV preview joins the dry run with the
/// values currently in force, and the export dumps the per-date override rows.
/// </summary>
internal sealed class PricingAdminReadService(
    AppDbContext dbContext,
    IDailyRateService dailyRates,
    IOptions<BookingPricingOptions> pricingOptions) : IPricingAdminReadService
{
    public async Task<IReadOnlyList<RatesMonthDto>> GetGridAsync(DateOnly firstMonth, int months, CancellationToken cancellationToken)
    {
        var from = new DateOnly(firstMonth.Year, firstMonth.Month, 1);
        var to = from.AddMonths(months);

        var overrides = (await dbContext.DailyRates.AsNoTracking()
            .Where(d => d.Date >= from && d.Date < to)
            .ToListAsync(cancellationToken))
            .ToDictionary(d => d.Date);
        var rules = await dbContext.PricingRules.AsNoTracking()
            .Where(r => r.StartDate < to && r.EndDate >= from)
            .OrderBy(r => r.StartDate)
            .ToListAsync(cancellationToken);

        var result = new List<RatesMonthDto>(months);
        for (var month = from; month < to; month = month.AddMonths(1))
        {
            var monthEnd = month.AddMonths(1);
            var days = new List<RateDayDto>(31);
            for (var date = month; date < monthEnd; date = date.AddDays(1))
            {
                days.Add(ResolveDay(date, overrides, rules));
            }

            result.Add(new RatesMonthDto(month, days));
        }

        return result;
    }

    public async Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(CancellationToken cancellationToken) =>
        await dbContext.PricingRules.AsNoTracking()
            .OrderBy(r => r.StartDate)
            .Select(r => new SeasonDto(
                r.Id.Value, r.StartDate, r.EndDate, r.NightlyRate.Amount, r.MinNights,
                r.WeeklyDiscountPct, r.MonthlyDiscountPct))
            .ToListAsync(cancellationToken);

    public async Task<FeesDto> GetFeesAsync(CancellationToken cancellationToken)
    {
        var fees = await dbContext.Fees.AsNoTracking().ToListAsync(cancellationToken);
        var cleaning = fees.Where(f => f.Type == FeeType.Cleaning).Sum(f => f.Amount.Amount);
        var tax = fees.FirstOrDefault(f => f.Type == FeeType.TouristTax);
        return new FeesDto(cleaning, tax?.Amount.Amount, tax?.MaxNights);
    }

    public async Task<IReadOnlyList<CancellationTier>> GetPolicyTiersAsync(CancellationToken cancellationToken)
    {
        var policy = await dbContext.CancellationPolicies.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return policy?.Tiers ?? [];
    }

    public async Task<IReadOnlyList<PromoDto>> GetPromosAsync(CancellationToken cancellationToken) =>
        await dbContext.PromoCodes.AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new PromoDto(p.Id.Value, p.Code, p.DiscountPct, p.IsActive, p.ValidFrom, p.ValidUntil))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<RejectionDto>> GetOpenRejectionsAsync(CancellationToken cancellationToken) =>
        await dbContext.RateSyncRejections.AsNoTracking()
            .Where(r => r.Status == RateSyncRejectionStatus.Open)
            .OrderBy(r => r.Date)
            .Select(r => new RejectionDto(
                r.Id.Value, r.Date, r.OfferedPriceEur, r.OfferedMinNights, r.Provider, r.Reason, r.DetectedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CsvPreviewRowDto>> PreviewCsvAsync(string csv, CancellationToken cancellationToken)
    {
        var preview = await dailyRates.PreviewCsvAsync(csv, cancellationToken);
        if (preview.Count == 0)
        {
            return [];
        }

        var valid = preview.Where(p => p.Status == "set").Select(p => p.Date).ToList();
        Dictionary<DateOnly, DailyRate> overrides = valid.Count == 0
            ? []
            : (await dbContext.DailyRates.AsNoTracking()
                .Where(d => valid.Contains(d.Date))
                .ToListAsync(cancellationToken))
                .ToDictionary(d => d.Date);
        List<PricingRule> rules = valid.Count == 0
            ? []
            : await dbContext.PricingRules.AsNoTracking()
                .Where(r => r.StartDate <= valid.Max() && r.EndDate >= valid.Min())
                .ToListAsync(cancellationToken);

        return preview.Select(p =>
        {
            if (p.Status != "set")
            {
                return new CsvPreviewRowDto(p.Status, null, null, null, null, null, "—", false);
            }

            var current = ResolveDay(p.Date, overrides, rules);
            var changes = current.PriceEur != p.Price || (p.MinNights is not null && current.MinNights != p.MinNights);
            return new CsvPreviewRowDto(
                p.Status, p.Date, p.Price, p.MinNights,
                current.PriceEur, current.MinNights, current.Source, changes);
        }).ToList();
    }

    public async Task<string> ExportRatesCsvAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.DailyRates.AsNoTracking()
            .OrderBy(d => d.Date)
            .Select(d => new { d.Date, d.NightlyRate, d.MinNights })
            .ToListAsync(cancellationToken);

        var sb = new StringBuilder("date,price,min_nights\n");
        foreach (var row in rows)
        {
            sb.Append(row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
              .Append(',')
              .Append(row.NightlyRate.Amount.ToString("0.##", CultureInfo.InvariantCulture))
              .Append(',')
              .Append(row.MinNights?.ToString(CultureInfo.InvariantCulture))
              .Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>Same per-night resolution as <see cref="PricingCalculator"/>: per-date rate → season → base.</summary>
    private RateDayDto ResolveDay(DateOnly date, Dictionary<DateOnly, DailyRate> overrides, IReadOnlyList<PricingRule> rules)
    {
        var rule = rules.FirstOrDefault(r => r.Covers(date));
        if (overrides.TryGetValue(date, out var daily))
        {
            return new RateDayDto(
                date, daily.NightlyRate.Amount,
                daily.MinNights ?? rule?.MinNights ?? pricingOptions.Value.StandardMinNights,
                daily.Source.ToString(), HasOverride: true, daily.IsLockedByOwner);
        }

        if (rule is not null)
        {
            return new RateDayDto(date, rule.NightlyRate.Amount, rule.MinNights, "Season", HasOverride: false, Locked: false);
        }

        var basePrice = pricingOptions.Value.BaseNightlyRateEur;
        return new RateDayDto(
            date, basePrice > 0 ? basePrice : null,
            pricingOptions.Value.StandardMinNights, "Base", HasOverride: false, Locked: false);
    }
}
