using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Web.Localization;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner content editor at /admin/content: sections and blocks, five language tabs per block with
/// draft/publish, a translation-status grid, and a preview link that renders drafts on the public site.
/// Builds on the existing <see cref="IAdminContentService"/> writes — it does not re-implement them.
/// </summary>
[Route("admin/content")]
public sealed class ContentAdminController(
    IAdminContentReadService read,
    IAdminContentService content,
    IAdminMediaReadService mediaRead,
    IAuditLog audit) : AdminControllerBase
{
    // Where each section is shown on the public site, used to build the preview link ("/{slug}" + suffix).
    private static readonly Dictionary<string, string> SectionPreviewSuffix =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["home"] = "",
            ["rules"] = "",
            ["faq"] = "",
            ["amenities"] = "",
            ["footer"] = "",
        };

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Content";
        var overview = await read.GetOverviewAsync(cancellationToken);
        return View(overview);
    }

    [HttpGet("grid")]
    public async Task<IActionResult> Grid(string? section, string? status, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Translation status";
        var overview = await read.GetOverviewAsync(cancellationToken);
        ViewData["Section"] = section;
        ViewData["Status"] = status;
        return View(overview);
    }

    [HttpGet("edit/{key}")]
    public async Task<IActionResult> Edit(string key, CancellationToken cancellationToken)
    {
        var block = await read.GetBlockAsync(key, cancellationToken);
        if (block is null)
        {
            return NotFound();
        }

        ViewData["Title"] = block.DisplayName;
        ViewData["Assets"] = block.Kind switch
        {
            ContentKind.ImageRef => await read.GetAssetsAsync(MediaKind.Image, cancellationToken),
            ContentKind.VideoRef => await read.GetAssetsAsync(MediaKind.Video, cancellationToken),
            _ => null,
        };
        ViewData["PreviewBase"] = PreviewPathFor(block.Section);
        return View(block);
    }

    [HttpPost("edit/{key}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string key, ContentEditForm form, CancellationToken cancellationToken)
    {
        var block = await read.GetBlockAsync(key, cancellationToken);
        if (block is null)
        {
            return NotFound();
        }

        var edits = form.Languages
            .Select(input => new ContentTranslationEdit(input.LanguageCode, ToValue(block.Kind, input)))
            .ToList();

        if (!await content.SetTranslationsAsync(key, edits, ActorName, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("content.draft.save", ActorId, Ip, "ContentBlock", key,
            new { languages = edits.Count }, cancellationToken);

        var publishing = string.Equals(form.Action, "publish", StringComparison.OrdinalIgnoreCase);

        // Gate: an image can't go on a public page without alt text in every language. The draft is saved
        // either way; only the publish is blocked so the owner keeps their edit and fixes the alt text.
        if (publishing && block.Kind == ContentKind.ImageRef
            && await FirstImageMissingAltAsync(form, cancellationToken) is { } unreadyAsset)
        {
            Toast($"Add alt text in every language for the selected image before publishing — manage it under Media ({unreadyAsset}).", "error");
            return LocalRedirect($"/admin/content/edit/{Uri.EscapeDataString(key)}");
        }

        if (publishing)
        {
            await content.PublishAsync(key, ActorName, cancellationToken);
            await audit.WriteAsync("content.publish", ActorId, Ip, "ContentBlock", key, null, cancellationToken);
            Toast($"Published “{block.DisplayName}”. The live site now shows it.");
        }
        else
        {
            Toast($"Saved draft for “{block.DisplayName}”. Publish to make it live.");
        }

        return LocalRedirect($"/admin/content/edit/{Uri.EscapeDataString(key)}");
    }

    /// <summary>The first selected image lacking alt in every language, or null when all are ready.</summary>
    private async Task<Guid?> FirstImageMissingAltAsync(ContentEditForm form, CancellationToken cancellationToken)
    {
        var assetIds = form.Languages
            .Where(l => l.MediaAssetId.HasValue)
            .Select(l => l.MediaAssetId!.Value)
            .Distinct();

        foreach (var assetId in assetIds)
        {
            if (!await mediaRead.IsReadyForPublicAsync(assetId, cancellationToken))
            {
                return assetId;
            }
        }

        return null;
    }

    [HttpPost("edit/{key}/unpublish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(string key, CancellationToken cancellationToken)
    {
        if (!await content.SetPublishedAsync(key, published: false, ActorName, cancellationToken))
        {
            return NotFound();
        }

        await audit.WriteAsync("content.unpublish", ActorId, Ip, "ContentBlock", key, null, cancellationToken);
        Toast("Unpublished. It is hidden from the public site until you publish again.", "info");
        return LocalRedirect($"/admin/content/edit/{Uri.EscapeDataString(key)}");
    }

    private static ContentValueDto ToValue(ContentKind kind, ContentLanguageInput input) => kind switch
    {
        ContentKind.Number => new ContentValueDto(null, input.Number, null, null),
        ContentKind.Boolean => new ContentValueDto(input.Text, null, input.Boolean, null),
        ContentKind.ImageRef or ContentKind.VideoRef => new ContentValueDto(null, null, null, input.MediaAssetId),
        _ => new ContentValueDto(input.Text, null, null, null),
    };

    /// <summary>The public path (without a language slug) a section previews on; null when it has no public page.</summary>
    private static string? PreviewPathFor(string section) =>
        SectionPreviewSuffix.TryGetValue(section, out var suffix) ? suffix : null;
}
