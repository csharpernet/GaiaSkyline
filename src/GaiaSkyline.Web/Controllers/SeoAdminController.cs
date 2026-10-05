using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner SEO tools at /admin/seo. Stage 7 §5 starts with the redirects manager (add/list/delete with loop
/// detection on save); per-page meta, the sitemap preview, Core Web Vitals and the warnings list follow.
/// </summary>
[Route("admin/seo")]
public sealed class SeoAdminController(IRedirectAdminService redirects, IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "SEO";
        var items = await redirects.GetAllAsync(cancellationToken);
        return View(items);
    }

    [HttpPost("redirects")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRedirect(string fromPath, string toPath, bool permanent, CancellationToken cancellationToken)
    {
        var result = await redirects.AddAsync(fromPath, toPath, permanent, ActorName, cancellationToken);
        if (!result.Ok)
        {
            Toast(result.Error ?? "Could not add the redirect.", "error");
            return LocalRedirect("/admin/seo");
        }

        await audit.WriteAsync("seo.redirect.add", ActorId, Ip, "Redirect", result.Id!.ToString(),
            new { fromPath, toPath, permanent }, cancellationToken);
        Toast("Redirect added.");
        return LocalRedirect("/admin/seo");
    }

    [HttpPost("redirects/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRedirect(Guid id, CancellationToken cancellationToken)
    {
        if (!await redirects.DeleteAsync(id, ActorName, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("seo.redirect.delete", ActorId, Ip, "Redirect", id.ToString(), null, cancellationToken);
        Toast("Redirect removed.");
        return LocalRedirect("/admin/seo");
    }
}
