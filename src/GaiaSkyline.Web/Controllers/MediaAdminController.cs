using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner media manager at /admin/media: the library grid by kind, per-language alt-text editing with the
/// "ready for public use" gate, and a "where used" list. Uploads reuse the existing <c>/api/admin/media</c>
/// endpoint. Controller named MediaAdmin to avoid colliding with the API's AdminMedia/Media controllers.
/// </summary>
[Route("admin/media")]
public sealed class MediaAdminController(
    IAdminMediaReadService read,
    IAdminMediaService media,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? kind, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Media";
        ViewData["Kind"] = kind;
        var filter = ParseKind(kind);
        var items = await read.GetLibraryAsync(filter, cancellationToken);
        return View(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var detail = await read.GetAssetAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Media asset";
        return View(detail);
    }

    [HttpPost("{id:guid}/alt")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAlt(Guid id, MediaAltForm form, CancellationToken cancellationToken)
    {
        var alts = form.Alts
            .GroupBy(a => a.LanguageCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Text, StringComparer.OrdinalIgnoreCase);

        if (!await media.SetAltTextsAsync(id, alts, ActorName, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("media.alt.set", ActorId, Ip, "MediaAsset", id.ToString(),
            new { languages = alts.Count }, cancellationToken);
        Toast("Alt text saved.");
        return LocalRedirect($"/admin/media/{id}");
    }

    private static MediaKind? ParseKind(string? kind) => kind?.ToLowerInvariant() switch
    {
        "image" => MediaKind.Image,
        "video" => MediaKind.Video,
        _ => null,
    };
}
