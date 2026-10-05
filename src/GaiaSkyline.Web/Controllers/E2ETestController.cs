using GaiaSkyline.Web.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// Test-only endpoint for the Playwright E2E suite (reads captured email). Returns 404 unless the E2E
/// seam is explicitly enabled (<c>E2E:Enabled</c>), which never happens in Production.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("test")]
[OutputCache(NoStore = true)]
public sealed class E2ETestController(IOptions<E2EOptions> options, E2EEmailSink sink) : ControllerBase
{
    [HttpGet("emails")]
    public IActionResult Emails() =>
        options.Value.Enabled ? Ok(sink.Snapshot()) : NotFound();

    /// <summary>A tiny deterministic ICS feed for the settings "Test fetch" spec (Stage 7 §12).</summary>
    [HttpGet("fixture.ics")]
    public IActionResult FixtureIcs()
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(500);
        static string D(DateOnly d) => d.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var ics = string.Join("\r\n",
            "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//GaiaSkyline-E2E//v1//EN",
            "BEGIN:VEVENT", "UID:e2e-1@fixture", $"DTSTART;VALUE=DATE:{D(start)}", $"DTEND;VALUE=DATE:{D(start.AddDays(3))}", "SUMMARY:E2E block one", "END:VEVENT",
            "BEGIN:VEVENT", "UID:e2e-2@fixture", $"DTSTART;VALUE=DATE:{D(start.AddDays(10))}", $"DTEND;VALUE=DATE:{D(start.AddDays(12))}", "SUMMARY:E2E block two", "END:VEVENT",
            "END:VCALENDAR", string.Empty);
        return Content(ics, "text/calendar");
    }

    /// <summary>Seeds a live hero video so the Playwright hero-video specs have one to exercise.</summary>
    [HttpPost("seed-hero")]
    public async Task<IActionResult> SeedHero(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        await E2ESeeder.SeedHeroAsync(HttpContext.RequestServices, cancellationToken);
        return Ok();
    }
}
