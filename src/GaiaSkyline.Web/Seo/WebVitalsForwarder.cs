using Microsoft.ApplicationInsights;

namespace GaiaSkyline.Web.Seo;

/// <summary>
/// Forwards each Core Web Vitals field sample to the durable telemetry sink (Stage 8 Part B). In production
/// that is Application Insights, where the 7-day p75 field data is queried and the LCP alert is defined
/// (Part D); in dev/CI it is a no-op and only the in-memory <see cref="IWebVitalsStore"/> (the live admin
/// panel) records the sample.
/// </summary>
public interface IWebVitalsForwarder
{
    void Report(string name, double value, string? rating, string? path);
}

/// <summary>Dev/CI default: the in-memory store is the only sink.</summary>
public sealed class NoOpWebVitalsForwarder : IWebVitalsForwarder
{
    public void Report(string name, double value, string? rating, string? path)
    {
    }
}

/// <summary>
/// Sends each sample to Application Insights as a custom metric named <c>WebVital.{name}</c> with the page
/// path and rating as dimensions, so p75-per-URL-over-7-days is queryable there and drives the alerts.
/// </summary>
public sealed class AppInsightsWebVitalsForwarder(TelemetryClient telemetry) : IWebVitalsForwarder
{
    public void Report(string name, double value, string? rating, string? path)
    {
        var metric = telemetry.GetMetric($"WebVital.{name}", "path", "rating");
        metric.TrackValue(value, path ?? "/", rating ?? "unknown");
    }
}
