using System.Security.Claims;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OutputCaching;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// Base for every admin section controller: Owner policy (role + 2FA + IP allowlist), never cached, and
/// noindex on every view. The auth controller (/admin/login etc.) is deliberately separate because its
/// pages are anonymous. Derived controllers add their own <c>[Route("admin/…")]</c>.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.Owner)]
[OutputCache(NoStore = true)]
public abstract class AdminControllerBase : Controller
{
    protected string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    protected Guid? ActorId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    protected string ActorName => User.Identity?.Name ?? "owner";

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ViewData["NoIndex"] = true;
        base.OnActionExecuting(context);
    }

    /// <summary>Queues a toast shown on the next rendered admin page (survives a redirect via TempData).</summary>
    protected void Toast(string message, string kind = "success") => TempData["Toast"] = $"{kind}:{message}";
}
