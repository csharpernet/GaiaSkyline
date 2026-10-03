using System.Globalization;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Pricing;

/// <summary>
/// Owner-managed per-date rates (manual entry + CSV). Every mutation bumps the content revision (which
/// invalidates the output cache and the JSON-LD "from €X") and the availability cache. Quotes always
/// read these stored rates live, so changes take effect on the next quote.
/// </summary>
internal sealed class DailyRateService(
    AppDbContext dbContext,
    IContentRevision revision,
    IAvailabilityService availabilityService,
    TimeProvider clock) : IDailyRateService
{
    public async Task SetRangeAsync(
        DateOnly fromInclusive, DateOnly toInclusive, decimal nightlyRateEur, int? minNights, string actor, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var money = new Money(nightlyRateEur, "EUR");
        var existing = await LoadRangeAsync(fromInclusive, toInclusive, cancellationToken);

        for (var date = fromInclusive; date <= toInclusive; date = date.AddDays(1))
        {
            if (existing.TryGetValue(date, out var rate))
            {
                rate.SetRate(money, minNights, RateSource.Manual, now, actor);
            }
            else
            {
                dbContext.DailyRates.Add(new DailyRate(date, money, minNights, RateSource.Manual, now, actor));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCaches();
    }

    public async Task<int> ClearRangeAsync(DateOnly fromInclusive, DateOnly toInclusive, string actor, CancellationToken cancellationToken)
    {
        var existing = await dbContext.DailyRates
            .Where(d => d.Date >= fromInclusive && d.Date <= toInclusive)
            .ToListAsync(cancellationToken);
        dbContext.DailyRates.RemoveRange(existing);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCaches();
        return existing.Count;
    }

    public async Task<int> SetLockedRangeAsync(DateOnly fromInclusive, DateOnly toInclusive, bool locked, string actor, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await dbContext.DailyRates
            .Where(d => d.Date >= fromInclusive && d.Date <= toInclusive)
            .ToListAsync(cancellationToken);
        foreach (var rate in existing)
        {
            rate.SetLocked(locked, now, actor);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCaches();
        return existing.Count;
    }

    public Task<IReadOnlyList<RatePreviewRow>> PreviewCsvAsync(string csv, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RatePreviewRow>>(ParseCsv(csv));

    public async Task<int> ApplyCsvAsync(string csv, string actor, CancellationToken cancellationToken)
    {
        var rows = ParseCsv(csv).Where(r => r.Status == "set").ToList();
        if (rows.Count == 0)
        {
            return 0;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var dates = rows.Select(r => r.Date).ToHashSet();
        var existing = (await dbContext.DailyRates.Where(d => dates.Contains(d.Date)).ToListAsync(cancellationToken))
            .ToDictionary(d => d.Date);

        foreach (var row in rows)
        {
            var money = new Money(row.Price, "EUR");
            if (existing.TryGetValue(row.Date, out var rate))
            {
                rate.SetRate(money, row.MinNights, RateSource.Manual, now, actor);
            }
            else
            {
                dbContext.DailyRates.Add(new DailyRate(row.Date, money, row.MinNights, RateSource.Manual, now, actor));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCaches();
        return rows.Count;
    }

    private async Task<Dictionary<DateOnly, DailyRate>> LoadRangeAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
        (await dbContext.DailyRates.Where(d => d.Date >= from && d.Date <= to).ToListAsync(cancellationToken))
            .ToDictionary(d => d.Date);

    private void InvalidateCaches()
    {
        revision.Bump();
        availabilityService.Invalidate();
    }

    private static List<RatePreviewRow> ParseCsv(string csv)
    {
        var rows = new List<RatePreviewRow>();
        if (string.IsNullOrWhiteSpace(csv))
        {
            return rows;
        }

        foreach (var raw in csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var cells = raw.Split(',', StringSplitOptions.TrimEntries);
            // Skip a header row ("date,...").
            if (cells[0].Equals("date", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (cells.Length < 2
                || !DateOnly.TryParse(cells[0], CultureInfo.InvariantCulture, out var date)
                || !decimal.TryParse(cells[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                || price <= 0)
            {
                rows.Add(new RatePreviewRow(default, 0, null, $"invalid: {raw}"));
                continue;
            }

            int? minNights = null;
            if (cells.Length >= 3 && !string.IsNullOrWhiteSpace(cells[2]))
            {
                if (int.TryParse(cells[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mn) && mn >= 1)
                {
                    minNights = mn;
                }
                else
                {
                    rows.Add(new RatePreviewRow(date, price, null, $"invalid: min_nights '{cells[2]}'"));
                    continue;
                }
            }

            rows.Add(new RatePreviewRow(date, price, minNights, "set"));
        }

        return rows;
    }
}
