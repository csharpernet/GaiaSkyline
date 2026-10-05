namespace GaiaSkyline.Web.Seo;

/// <summary>The p75 of one Core Web Vital over the recent in-memory samples, with its good/poor classification.</summary>
public sealed record WebVitalSummary(string Name, double P75, int SampleCount, string Rating, string Unit);

/// <summary>
/// Collects Core Web Vitals field samples reported by the browser (<c>/api/vitals</c>) and exposes a rolling
/// p75 per metric for the owner's SEO dashboard. In-memory and best-effort: it is a process-local rolling
/// window (lost on restart), enough for a quick read in the admin; production forwards to a real sink in
/// Stage 8. Stage 7 §5.
/// </summary>
public interface IWebVitalsStore
{
    /// <summary>Record one field sample. Unknown metric names are ignored.</summary>
    void Record(string name, double value);

    /// <summary>The p75 summary for each metric that has samples, in a stable display order.</summary>
    IReadOnlyList<WebVitalSummary> Snapshot();
}

/// <summary>Thread-safe in-memory <see cref="IWebVitalsStore"/> keeping the last N samples per metric.</summary>
public sealed class WebVitalsStore : IWebVitalsStore
{
    private const int MaxSamplesPerMetric = 1000;

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

    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<double>> _samples = new(StringComparer.OrdinalIgnoreCase);

    public void Record(string name, double value)
    {
        if (string.IsNullOrWhiteSpace(name) || double.IsNaN(value) || double.IsInfinity(value)
            || !SpecByName.TryGetValue(name, out var spec))
        {
            return;
        }

        var key = spec.Name; // canonical casing
        lock (_gate)
        {
            if (!_samples.TryGetValue(key, out var queue))
            {
                queue = new Queue<double>(MaxSamplesPerMetric);
                _samples[key] = queue;
            }

            queue.Enqueue(value);
            while (queue.Count > MaxSamplesPerMetric)
            {
                queue.Dequeue();
            }
        }
    }

    public IReadOnlyList<WebVitalSummary> Snapshot()
    {
        var result = new List<WebVitalSummary>(Specs.Count);
        foreach (var spec in Specs)
        {
            double[] values;
            lock (_gate)
            {
                if (!_samples.TryGetValue(spec.Name, out var queue) || queue.Count == 0)
                {
                    continue;
                }

                values = queue.ToArray();
            }

            Array.Sort(values);
            var p75 = Percentile(values, 0.75);
            result.Add(new WebVitalSummary(spec.Name, p75, values.Length, spec.Classify(p75), spec.Unit));
        }

        return result;
    }

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

    private sealed record MetricSpec(string Name, double Good, double Poor, string Unit)
    {
        public string Classify(double value) =>
            value <= Good ? "good" : value <= Poor ? "needs-improvement" : "poor";
    }
}
