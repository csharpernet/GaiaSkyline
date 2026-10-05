using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Seo;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner SEO tools at /admin/seo. Stage 7 §5: the redirects manager (add/list/delete with loop detection)
/// and per-page, per-language meta overrides. Sitemap preview, Core Web Vitals and warnings follow.
/// </summary>
[Route("admin/seo")]
public sealed class SeoAdminController(
    IRedirectAdminService redirects,
    IPageMetaAdminService pageMeta,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "SEO";
        var model = new SeoAdminViewModel(
            await redirects.GetAllAsync(cancellationToken),
            await pageMeta.GetAllAsync(cancellationToken));
        return View(model);
    }

    [HttpPost("page-meta")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePageMeta(List<PageMetaForm> items, CancellationToken cancellationToken)
    {
        var changed = 0;
        foreach (var item in items ?? [])
        {
            if (await pageMeta.UpsertAsync(item.PageKey, item.LanguageCode, item.Title, item.Description, ActorName, cancellationToken))
            {
                changed++;
            }
        }

        await audit.WriteAsync("seo.pagemeta.save", ActorId, Ip, "PageMetaOverride", null, new { count = changed }, cancellationToken);
        Toast("Page meta saved.");
        return LocalRedirect("/admin/seo");
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
