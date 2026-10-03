using System.Security.Claims;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Availability;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

public sealed record CreateOwnerBlockRequest(DateOnly StartDate, DateOnly EndDate, OwnerBlockKind Kind, string? Note);

/// <summary>
/// Owner-only manual calendar blocks (admin UI lands in Stage 7). Cookie-authenticated; SameSite=Lax
/// covers CSRF. Mutations are audited and invalidate the availability + ICS caches.
/// </summary>
[ApiController]
[Route("api/admin/owner-blocks")]
[Authorize(Policy = AuthorizationPolicies.Owner)]
public sealed class AdminOwnerBlocksController(IOwnerBlockService ownerBlocks, IAuditLog audit) : ControllerBase
{
    private Guid? ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string Actor => User.Identity?.Name ?? "owner";

    private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var effectiveTo = to <= from ? from.AddYears(2) : to;
        var blocks = await ownerBlocks.ListAsync(from, effectiveTo, cancellationToken);
        return Ok(blocks);
    }

    [HttpGet("duplicates")]
    public async Task<IActionResult> Duplicates(CancellationToken cancellationToken) =>
        Ok(await ownerBlocks.ListImportedDuplicatesAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOwnerBlockRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var id = await ownerBlocks.CreateAsync(
                request.StartDate, request.EndDate, request.Kind, request.Note, Actor, cancellationToken);
            await audit.WriteAsync("owner_block.create", ActorId, Ip, "OwnerBlock", id.ToString(),
                new { request.StartDate, request.EndDate, request.Kind }, cancellationToken);
            return Ok(new { id });
        }
        catch (OwnerBlockConflictsWithBookingException ex)
        {
            return Conflict(new { error = ex.Message, bookingReference = ex.BookingReference });
        }
        catch (ArgumentException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!await ownerBlocks.DeleteAsync(id, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("owner_block.delete", ActorId, Ip, "OwnerBlock", id.ToString(), null, cancellationToken);
        return NoContent();
    }
}
