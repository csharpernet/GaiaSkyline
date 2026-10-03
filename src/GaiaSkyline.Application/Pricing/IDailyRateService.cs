namespace GaiaSkyline.Application.Pricing;

/// <summary>One parsed CSV row for the import preview. Status is "set" or "invalid: &lt;reason&gt;".</summary>
public sealed record RatePreviewRow(DateOnly Date, decimal Price, int? MinNights, string Status);

/// <summary>
/// Owner-managed per-date rates. Every mutation invalidates the caches that show prices (quotes read
/// stored rates live; the output cache / JSON-LD "from €X" is bumped). Imports are a separate path.
/// </summary>
public interface IDailyRateService
{
    /// <summary>Sets a manual nightly rate (and optional min nights) for every date in [from, to] (inclusive).</summary>
    Task SetRangeAsync(DateOnly fromInclusive, DateOnly toInclusive, decimal nightlyRateEur, int? minNights, string actor, CancellationToken cancellationToken);

    /// <summary>Removes any per-date rates in [from, to] (inclusive), falling back to seasons/base.</summary>
    Task<int> ClearRangeAsync(DateOnly fromInclusive, DateOnly toInclusive, string actor, CancellationToken cancellationToken);

    /// <summary>Locks/unlocks existing per-date rates in [from, to] (locked dates are never overwritten by imports).</summary>
    Task<int> SetLockedRangeAsync(DateOnly fromInclusive, DateOnly toInclusive, bool locked, string actor, CancellationToken cancellationToken);

    /// <summary>Parses a CSV (date,price[,min_nights]) and returns the would-be changes without applying them.</summary>
    Task<IReadOnlyList<RatePreviewRow>> PreviewCsvAsync(string csv, CancellationToken cancellationToken);

    /// <summary>Applies the valid rows of a CSV as manual rates; returns the number applied.</summary>
    Task<int> ApplyCsvAsync(string csv, string actor, CancellationToken cancellationToken);
}
