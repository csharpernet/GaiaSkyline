using System.Security.Claims;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>
/// Owner content writes. Cookie-authenticated (SameSite=Lax blocks cross-site POST/PUT, so CSRF is
/// covered); the admin UI arrives in Stage 7. Every write bumps the content revision and is audited.
/// </summary>
[ApiController]
[Route("api/admin/content")]
[Authorize(Policy = AuthorizationPolicies.Owner)]
public sealed class AdminContentController(IAdminContentService content, IAuditLog audit) : ControllerBase
{
    private string Actor => User.Identity?.Name ?? "owner";

    private Guid? ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpPut("{key}/translations/{lang}")]
    public async Task<IActionResult> SetTranslation(string key, string lang, [FromBody] ContentValueDto value, CancellationToken cancellationToken)
    {
        if (!await content.SetTranslationAsync(key, lang, value, Actor, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("content.translation.set", ActorId, Ip, "ContentBlock", key, new { lang }, cancellationToken);
        return NoContent();
    }

    [HttpPost("{key}/publish")]
    public async Task<IActionResult> Publish(string key, CancellationToken cancellationToken)
    {
        // Promotes pending drafts to the published values and makes the block live.
        if (!await content.PublishAsync(key, Actor, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("content.publish", ActorId, Ip, "ContentBlock", key, null, cancellationToken);
        return NoContent();
    }

    [HttpPost("{key}/unpublish")]
    public async Task<IActionResult> Unpublish(string key, CancellationToken cancellationToken)
    {
        if (!await content.SetPublishedAsync(key, published: false, Actor, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("content.unpublish", ActorId, Ip, "ContentBlock", key, null, cancellationToken);
        return NoContent();
    }
}
