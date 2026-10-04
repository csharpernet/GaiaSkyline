using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner media manager at /admin/media: the library grid by kind, per-language alt-text editing with the
/// "ready for public use" gate, a "where used" list, replace-keeping-id and soft delete. Uploads reuse the
/// existing <c>/api/admin/media</c> endpoint. Named MediaAdmin to avoid the API's AdminMedia/Media controllers.
/// </summary>
[Route("admin/media")]
public sealed class MediaAdminController(
    IAdminMediaReadService read,
    IAdminMediaService media,
    IWebHostEnvironment environment,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? kind, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Media";
        ViewData["Kind"] = kind;
        var deletedView = string.Equals(kind, "deleted", StringComparison.OrdinalIgnoreCase);
        var filter = deletedView ? null : ParseKind(kind);
        var items = await read.GetLibraryAsync(filter, deletedView, cancellationToken);
        return View(items);
    }

    [HttpGet("gallery")]
    public async Task<IActionResult> Gallery(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Home gallery";
        var model = await read.GetCollectionAsync("home.gallery", cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
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

    [HttpPost("{id:guid}/replace")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Replace(Guid id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            Toast("Choose an image file to replace this asset.", "error");
            return LocalRedirect($"/admin/media/{id}");
        }

        await using var stream = file.OpenReadStream();
        if (!await media.ReplaceImageAsync(id, stream, MediaDirectory(), ActorName, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("media.replace", ActorId, Ip, "MediaAsset", id.ToString(), null, cancellationToken);
        Toast("Image replaced. Every page that uses it now shows the new file.");
        return LocalRedirect($"/admin/media/{id}");
    }

    [HttpPost("{id:guid}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(Guid id, string? newName, CancellationToken cancellationToken)
    {
        var result = await media.RenameImageAsync(id, newName ?? string.Empty, MediaDirectory(), ActorName, cancellationToken);
        switch (result)
        {
            case MediaRenameResult.NotFound:
                return NotFound();
            case MediaRenameResult.Unsupported:
                Toast("This asset can't be renamed.", "error");
                break;
            case MediaRenameResult.InvalidName:
                Toast("Enter a filename with at least one letter or number.", "error");
                break;
            case MediaRenameResult.Unchanged:
                Toast("That is already the filename.", "info");
                break;
            default:
                await audit.WriteAsync("media.rename", ActorId, Ip, "MediaAsset", id.ToString(),
                    new { newName }, cancellationToken);
                Toast("Filename updated. The old image URL now redirects to the new one.");
                break;
        }

        return LocalRedirect($"/admin/media/{id}");
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await media.SoftDeleteAsync(id, ActorName, cancellationToken);
        switch (result)
        {
            case MediaDeleteResult.NotFound:
                return NotFound();
            case MediaDeleteResult.InUse:
                Toast("Can't delete — this asset is still used on a page. Remove it there first.", "error");
                return LocalRedirect($"/admin/media/{id}");
            default:
                await audit.WriteAsync("media.delete", ActorId, Ip, "MediaAsset", id.ToString(), null, cancellationToken);
                Toast("Moved to deleted. You can restore it from the Deleted tab.", "info");
                return LocalRedirect("/admin/media");
        }
    }

    [HttpPost("{id:guid}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        if (!await media.RestoreAsync(id, ActorName, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("media.restore", ActorId, Ip, "MediaAsset", id.ToString(), null, cancellationToken);
        Toast("Restored.");
        return LocalRedirect($"/admin/media/{id}");
    }

    private string MediaDirectory() => Path.Combine(
        string.IsNullOrEmpty(environment.WebRootPath) ? "wwwroot" : environment.WebRootPath, "media");

    private static MediaKind? ParseKind(string? kind) => kind?.ToLowerInvariant() switch
    {
        "image" => MediaKind.Image,
        "video" => MediaKind.Video,
        _ => null,
    };
}
