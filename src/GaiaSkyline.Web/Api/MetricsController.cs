using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>Core Web Vitals field-metric sink. Dev stub logs them; production forwards to App Insights (Stage 8).</summary>
[ApiController]
[Route("api/vitals")]
public sealed partial class MetricsController(ILogger<MetricsController> logger) : ControllerBase
{
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Report([FromBody] WebVitalMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);
        LogVital(logger, metric.Name, metric.Value, metric.Rating, metric.Path);
        return NoContent();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "web-vital {Name}={Value} rating={Rating} path={Path}")]
    private static partial void LogVital(ILogger logger, string? name, double value, string? rating, string? path);
}

public sealed record WebVitalMetric(string? Name, double Value, string? Rating, string? Id, string? Path);
