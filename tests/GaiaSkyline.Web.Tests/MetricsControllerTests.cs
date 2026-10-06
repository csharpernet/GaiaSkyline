using FluentAssertions;
using GaiaSkyline.Web.Api;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// <c>/api/vitals</c> records each sample in the in-memory panel store AND forwards it to the durable sink
/// — Application Insights in production, a no-op in dev/CI (Stage 8 Part B).
/// </summary>
public sealed class MetricsControllerTests
{
    [Fact]
    public void Report_records_the_sample_and_forwards_it()
    {
        var store = new WebVitalsStore();
        var forwarder = new CapturingForwarder();
        var controller = new MetricsController(NullLogger<MetricsController>.Instance, store, forwarder);

        var result = controller.Report(new WebVitalMetric("LCP", 2100, "good", "id-1", "/en"));

        result.Should().BeOfType<NoContentResult>();
        forwarder.Calls.Should().ContainSingle()
            .Which.Should().Be(("LCP", 2100d, "good", "/en"));
        store.Snapshot().Should().ContainSingle(p => p.Path == "/en");
    }

    [Fact]
    public void An_empty_metric_name_is_ignored()
    {
        var forwarder = new CapturingForwarder();
        var controller = new MetricsController(NullLogger<MetricsController>.Instance, new WebVitalsStore(), forwarder);

        controller.Report(new WebVitalMetric("", 1, null, null, "/en")).Should().BeOfType<NoContentResult>();

        forwarder.Calls.Should().BeEmpty();
    }

    private sealed class CapturingForwarder : IWebVitalsForwarder
    {
        public List<(string Name, double Value, string? Rating, string? Path)> Calls { get; } = [];

        public void Report(string name, double value, string? rating, string? path) =>
            Calls.Add((name, value, rating, path));
    }
}
