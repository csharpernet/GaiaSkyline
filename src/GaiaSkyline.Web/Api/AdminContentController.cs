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
    public Task<IActionResult> Publish(string key, CancellationToken cancellationToken) =>
        SetPublishedAsync(key, true, cancellationToken);

    [HttpPost("{key}/unpublish")]
    public Task<IActionResult> Unpublish(string key, CancellationToken cancellationToken) =>
        SetPublishedAsync(key, false, cancellationToken);

    private async Task<IActionResult> SetPublishedAsync(string key, bool published, CancellationToken cancellationToken)
    {
        if (!await content.SetPublishedAsync(key, published, Actor, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync(published ? "content.publish" : "content.unpublish", ActorId, Ip, "ContentBlock", key, null, cancellationToken);
        return NoContent();
    }
}
