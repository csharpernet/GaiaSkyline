using System.Globalization;
using GaiaSkyline.Application.Bookings;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>Returns the blocked nights in a date window so the calendar can disable them.</summary>
[ApiController]
[Route("api/availability")]
public sealed class AvailabilityController(IAvailabilityService availabilityService) : ControllerBase
{
    private const int MaxWindowDays = 400;

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(typeof(AvailabilityResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Post([FromBody] AvailabilityApiRequest request, CancellationToken cancellationToken)
    {
        if (request is null || request.To <= request.From)
        {
            return BadRequest();
        }

        if (request.To.DayNumber - request.From.DayNumber > MaxWindowDays)
        {
            return BadRequest(new { error = "Requested range is too large." });
        }

        var blocked = await availabilityService.GetBlockedDatesAsync(request.From, request.To, cancellationToken);
        return Ok(new AvailabilityResponse(
            blocked.Select(d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).ToList()));
    }
}

public sealed record AvailabilityApiRequest(DateOnly From, DateOnly To);

public sealed record AvailabilityResponse(IReadOnlyList<string> BlockedDates);
