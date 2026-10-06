using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>
/// Core Web Vitals field-metric sink. Logs each sample and feeds the in-memory rolling store that the owner's
/// SEO dashboard reads (Stage 7 §5); production additionally forwards to App Insights (Stage 8).
/// </summary>
[ApiController]
[Route("api/vitals")]
public sealed partial class MetricsController(ILogger<MetricsController> logger, IWebVitalsStore vitals) : ControllerBase
{
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Report([FromBody] WebVitalMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        LogVital(logger, metric.Name, metric.Value, metric.Rating, metric.Path);
        if (!string.IsNullOrWhiteSpace(metric.Name))
        {
            // Group field data per URL (Stage 7 §5); fall back to the Referer when the client omits the path.
            var path = string.IsNullOrWhiteSpace(metric.Path)
                ? Uri.TryCreate(Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
                    ? referer.AbsolutePath
                    : null
                : metric.Path;
            vitals.Record(metric.Name, metric.Value, path);
        }

        return NoContent();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "web-vital {Name}={Value} rating={Rating} path={Path}")]
    private static partial void LogVital(ILogger logger, string? name, double value, string? rating, string? path);
}

public sealed record WebVitalMetric(string? Name, double Value, string? Rating, string? Id, string? Path);
