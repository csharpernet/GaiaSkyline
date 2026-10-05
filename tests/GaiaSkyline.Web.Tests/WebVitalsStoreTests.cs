using FluentAssertions;
using GaiaSkyline.Web.Seo;

namespace GaiaSkyline.Web.Tests;

/// <summary>The in-memory Core Web Vitals store computes a p75 per metric and classifies it. Stage 7 §5.</summary>
public sealed class WebVitalsStoreTests
{
    [Fact]
    public void Snapshot_reports_p75_rating_and_unit_per_metric()
    {
        var store = new WebVitalsStore();

        // 10 LCP samples 1000..10000 → nearest-rank p75 = ceil(0.75*10)=8 → index 7 → 8000ms → poor (> 4000).
        for (var i = 1; i <= 10; i++)
        {
            store.Record("LCP", i * 1000);
        }

        store.Record("CLS", 0.05);

        var snapshot = store.Snapshot();

        var lcp = snapshot.Single(s => s.Name == "LCP");
        lcp.P75.Should().Be(8000);
        lcp.SampleCount.Should().Be(10);
        lcp.Rating.Should().Be("poor");
        lcp.Unit.Should().Be("ms");

        var cls = snapshot.Single(s => s.Name == "CLS");
        cls.P75.Should().Be(0.05);
        cls.Rating.Should().Be("good");
        cls.Unit.Should().BeEmpty();
    }

    [Fact]
    public void Unknown_and_invalid_samples_are_ignored_and_empty_metrics_are_omitted()
    {
        var store = new WebVitalsStore();
        store.Record("NOT_A_METRIC", 123);
        store.Record("LCP", double.NaN);

        store.Snapshot().Should().BeEmpty("no valid samples were recorded");

        store.Record("INP", 150);
        var snapshot = store.Snapshot();
        snapshot.Should().ContainSingle();
        snapshot[0].Name.Should().Be("INP");
        snapshot[0].Rating.Should().Be("good"); // 150 <= 200
    }
}
