using FluentAssertions;
using GaiaSkyline.Web.Seo;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// The in-memory Core Web Vitals store groups field samples per URL, computes a p75 per metric over a 7-day
/// window and classifies it. Stage 7 §5.
/// </summary>
public sealed class WebVitalsStoreTests
{
    [Fact]
    public void Snapshot_reports_p75_rating_and_unit_per_metric_grouped_by_url()
    {
        var store = new WebVitalsStore();

        // 10 LCP samples 1000..10000 on /en → nearest-rank p75 = ceil(0.75*10)=8 → index 7 → 8000ms → poor.
        for (var i = 1; i <= 10; i++)
        {
            store.Record("LCP", i * 1000, "/en");
        }

        store.Record("CLS", 0.05, "/en");
        store.Record("LCP", 1200, "/en/gallery");

        var snapshot = store.Snapshot();
        snapshot.Should().HaveCount(2);

        // Worst-rated page first: /en is poor, /en/gallery is good.
        snapshot[0].Path.Should().Be("/en");
        var lcp = snapshot[0].Metrics.Single(s => s.Name == "LCP");
        lcp.P75.Should().Be(8000);
        lcp.SampleCount.Should().Be(10);
        lcp.Rating.Should().Be("poor");
        lcp.Unit.Should().Be("ms");

        var cls = snapshot[0].Metrics.Single(s => s.Name == "CLS");
        cls.P75.Should().Be(0.05);
        cls.Rating.Should().Be("good");
        cls.Unit.Should().BeEmpty();

        snapshot[1].Path.Should().Be("/en/gallery");
        snapshot[1].Metrics.Single(s => s.Name == "LCP").Rating.Should().Be("good");
    }

    [Fact]
    public void Unknown_and_invalid_samples_are_ignored_and_empty_metrics_are_omitted()
    {
        var store = new WebVitalsStore();
        store.Record("NOT_A_METRIC", 123, "/en");
        store.Record("LCP", double.NaN, "/en");

        store.Snapshot().Should().BeEmpty("no valid samples were recorded");

        store.Record("INP", 150, "/en?utm=x");
        var snapshot = store.Snapshot();
        snapshot.Should().ContainSingle();
        snapshot[0].Path.Should().Be("/en", "the query string is not part of the page identity");
        snapshot[0].Metrics.Single().Name.Should().Be("INP");
        snapshot[0].Metrics.Single().Rating.Should().Be("good"); // 150 <= 200
    }

    [Fact]
    public void Samples_older_than_seven_days_fall_out_of_the_window()
    {
        var time = new TestTime { Now = DateTimeOffset.Parse("2026-10-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture) };
        var store = new WebVitalsStore(time);

        store.Record("TTFB", 5000, "/en"); // would rate poor if it survived

        time.Now += TimeSpan.FromDays(8);
        store.Record("TTFB", 100, "/en");

        var snapshot = store.Snapshot();
        var ttfb = snapshot.Single().Metrics.Single(s => s.Name == "TTFB");
        ttfb.SampleCount.Should().Be(1, "the 8-day-old sample is outside the 7-day window");
        ttfb.P75.Should().Be(100);
        ttfb.Rating.Should().Be("good");
    }

    private sealed class TestTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
