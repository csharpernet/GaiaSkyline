using System.Security.Claims;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

public sealed record SetRatesRequest(DateOnly From, DateOnly To, decimal NightlyRate, int? MinNights);

public sealed record LockRatesRequest(DateOnly From, DateOnly To, bool Locked);

public sealed record CsvRequest(string Csv);

/// <summary>
/// Owner per-date pricing (admin calendar grid arrives in Stage 7). Cookie-auth; SameSite=Lax covers
/// CSRF. Every change is audited and invalidates the price caches.
/// </summary>
[ApiController]
[Route("api/admin/pricing")]
[Authorize(Policy = AuthorizationPolicies.Owner)]
public sealed class AdminPricingController(IDailyRateService rates, IAuditLog audit) : ControllerBase
{
    private Guid? ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string Actor => User.Identity?.Name ?? "owner";

    private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpPut("rates")]
    public async Task<IActionResult> SetRange([FromBody] SetRatesRequest request, CancellationToken cancellationToken)
    {
        if (request.To < request.From || request.NightlyRate <= 0)
        {
            return UnprocessableEntity(new { error = "Invalid range or rate." });
        }

        await rates.SetRangeAsync(request.From, request.To, request.NightlyRate, request.MinNights, Actor, cancellationToken);
        await audit.WriteAsync("pricing.set", ActorId, Ip, "DailyRate", $"{request.From}..{request.To}",
            new { request.NightlyRate, request.MinNights }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("rates")]
    public async Task<IActionResult> ClearRange([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var cleared = await rates.ClearRangeAsync(from, to, Actor, cancellationToken);
        await audit.WriteAsync("pricing.clear", ActorId, Ip, "DailyRate", $"{from}..{to}", new { cleared }, cancellationToken);
        return Ok(new { cleared });
    }

    [HttpPost("rates/lock")]
    public async Task<IActionResult> SetLocked([FromBody] LockRatesRequest request, CancellationToken cancellationToken)
    {
        var count = await rates.SetLockedRangeAsync(request.From, request.To, request.Locked, Actor, cancellationToken);
        await audit.WriteAsync(request.Locked ? "pricing.lock" : "pricing.unlock", ActorId, Ip, "DailyRate",
            $"{request.From}..{request.To}", new { count }, cancellationToken);
        return Ok(new { count });
    }

    [HttpPost("rates/csv")]
    public async Task<IActionResult> ImportCsv([FromQuery] bool apply, [FromBody] CsvRequest request, CancellationToken cancellationToken)
    {
        if (!apply)
        {
            // Dry-run preview: show the changes without applying them.
            var preview = await rates.PreviewCsvAsync(request.Csv, cancellationToken);
            return Ok(new { preview });
        }

        var applied = await rates.ApplyCsvAsync(request.Csv, Actor, cancellationToken);
        await audit.WriteAsync("pricing.csv_import", ActorId, Ip, details: new { applied }, cancellationToken: cancellationToken);
        return Ok(new { applied });
    }
}
