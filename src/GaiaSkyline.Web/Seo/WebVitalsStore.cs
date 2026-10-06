namespace GaiaSkyline.Web.Seo;

/// <summary>The p75 of one Core Web Vital over the recent in-memory samples, with its good/poor classification.</summary>
public sealed record WebVitalSummary(string Name, double P75, int SampleCount, string Rating, string Unit);

/// <summary>One page's Core Web Vitals: its site-relative path and the p75 per metric reported for it.</summary>
public sealed record WebVitalPageSummary(string Path, IReadOnlyList<WebVitalSummary> Metrics);

/// <summary>
/// Collects Core Web Vitals field samples reported by the browser (<c>/api/vitals</c>) and exposes the p75
/// per URL per metric over the last seven days for the owner's SEO dashboard (Stage 7 §5). In-memory and
/// best-effort: process-local, lost on restart, enough for a quick read in the admin; production forwards to
/// a real sink in Stage 8.
/// </summary>
public interface IWebVitalsStore
{
    /// <summary>Record one field sample for a page. Unknown metric names are ignored; a blank path is "/".</summary>
    void Record(string name, double value, string? path);

    /// <summary>Per-URL p75 summaries over the 7-day window, worst-rated pages first.</summary>
    IReadOnlyList<WebVitalPageSummary> Snapshot();
}

/// <summary>Thread-safe in-memory <see cref="IWebVitalsStore"/>: a 7-day rolling window per (path, metric).</summary>
public sealed class WebVitalsStore(TimeProvider? time = null) : IWebVitalsStore
{
    /// <summary>The field-data window the spec asks for (p75 over 7 days).</summary>
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);

    private const int MaxSamplesPerSeries = 1000;
    private const int MaxTrackedPaths = 200; // bound memory; beyond this new paths are dropped (best-effort)

    // The five metrics the client reports, in display order, with their good/poor thresholds (ms, except CLS).
    private static readonly IReadOnlyList<MetricSpec> Specs =
    [
        new("LCP", Good: 2500, Poor: 4000, Unit: "ms"),
        new("INP", Good: 200, Poor: 500, Unit: "ms"),
        new("CLS", Good: 0.1, Poor: 0.25, Unit: ""),
        new("FCP", Good: 1800, Poor: 3000, Unit: "ms"),
        new("TTFB", Good: 800, Poor: 1800, Unit: "ms"),
    ];

    private static readonly Dictionary<string, MetricSpec> SpecByName =
        Specs.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly object _gate = new();
    private readonly Dictionary<string, Dictionary<string, Queue<Sample>>> _byPath = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string name, double value, string? path)
    {
        if (string.IsNullOrWhiteSpace(name) || double.IsNaN(value) || double.IsInfinity(value)
            || !SpecByName.TryGetValue(name, out var spec))
        {
            return;
        }

        var page = NormalizePath(path);
        var now = _time.GetUtcNow().UtcDateTime;
        lock (_gate)
        {
            if (!_byPath.TryGetValue(page, out var metrics))
            {
                if (_byPath.Count >= MaxTrackedPaths)
                {
                    return;
                }

                metrics = new Dictionary<string, Queue<Sample>>(StringComparer.OrdinalIgnoreCase);
                _byPath[page] = metrics;
            }

            if (!metrics.TryGetValue(spec.Name, out var queue))
            {
                queue = new Queue<Sample>();
                metrics[spec.Name] = queue;
            }

            queue.Enqueue(new Sample(value, now));
            Prune(queue, now);
        }
    }

    public IReadOnlyList<WebVitalPageSummary> Snapshot()
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var pages = new List<(WebVitalPageSummary Page, int WorstRank)>();
        lock (_gate)
        {
            foreach (var (path, metrics) in _byPath)
            {
                var summaries = new List<WebVitalSummary>(Specs.Count);
                foreach (var spec in Specs)
                {
                    if (!metrics.TryGetValue(spec.Name, out var queue))
                    {
                        continue;
                    }

                    Prune(queue, now);
                    if (queue.Count == 0)
                    {
                        continue;
                    }

                    var values = queue.Select(s => s.Value).ToArray();
                    Array.Sort(values);
                    var p75 = Percentile(values, 0.75);
                    summaries.Add(new WebVitalSummary(spec.Name, p75, values.Length, spec.Classify(p75), spec.Unit));
                }

                if (summaries.Count > 0)
                {
                    var worst = summaries.Max(s => RatingRank(s.Rating));
                    pages.Add((new WebVitalPageSummary(path, summaries), worst));
                }
            }
        }

        return pages
            .OrderByDescending(p => p.WorstRank)
            .ThenBy(p => p.Page.Path, StringComparer.Ordinal)
            .Select(p => p.Page)
            .ToList();
    }

    private static void Prune(Queue<Sample> queue, DateTime now)
    {
        var cutoff = now - Window;
        while (queue.Count > 0 && (queue.Peek().AtUtc < cutoff || queue.Count > MaxSamplesPerSeries))
        {
            queue.Dequeue();
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var trimmed = path.Trim();
        var query = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            trimmed = trimmed[..query];
        }

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed.Length > 1 ? trimmed.TrimEnd('/').ToLowerInvariant() : "/";
    }

    private static int RatingRank(string rating) => rating switch
    {
        "poor" => 2,
        "needs-improvement" => 1,
        _ => 0,
    };

    // Nearest-rank percentile on an ascending-sorted array (the method the Web Vitals programme uses for p75).
    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 1)
        {
            return sorted[0];
        }

        var rank = (int)Math.Ceiling(percentile * sorted.Length);
        var index = Math.Clamp(rank - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private readonly record struct Sample(double Value, DateTime AtUtc);

    private sealed record MetricSpec(string Name, double Good, double Poor, string Unit)
    {
        public string Classify(double value) =>
            value <= Good ? "good" : value <= Poor ? "needs-improvement" : "poor";
    }
}
