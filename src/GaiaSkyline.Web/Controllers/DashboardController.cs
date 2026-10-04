using System.Globalization;
using GaiaSkyline.Application.Admin;
using GaiaSkyline.Application.Pricing;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>The owner dashboard at /admin: KPIs, the colour-coded month calendar, sync panels, the
/// manual Hostify-sync to-do, open conflicts and Multibanco holds.</summary>
[Route("admin")]
public sealed class DashboardController(IDashboardService dashboard, TimeProvider clock) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? month, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Dashboard";
        var summary = await dashboard.GetAsync(ResolveMonth(month), cancellationToken);
        return View(summary);
    }

    [HttpPost("sync/{reference}/done")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSynced(string reference, string? note, CancellationToken cancellationToken)
    {
        await dashboard.MarkSyncedAsync(reference, note, ActorId, Ip, cancellationToken);
        Toast($"Marked booking {reference.ToUpperInvariant()} as mirrored in Hostify.");
        return LocalRedirect("/admin");
    }

    private DateOnly ResolveMonth(string? month) =>
        DateOnly.TryParseExact($"{month}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : new DateOnly(LisbonClock.Today(clock).Year, LisbonClock.Today(clock).Month, 1);
}
