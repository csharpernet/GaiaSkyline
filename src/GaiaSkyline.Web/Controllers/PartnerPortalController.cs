using System.IO.Compression;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Web.Localization;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Security;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The partner dashboard (Stage 8 Part A): month-vs-last stats, attributed bookings (guest first names
/// only), payouts with PDF statements, the referral-link builder, the media kit and the payout-details
/// profile. Partner-role login, noindex, never cached.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.Partner)]
[OutputCache(NoStore = true)]
public sealed class PartnerPortalController(
    IPartnerDashboardService dashboard,
    IPartnerStatementPdfService statements,
    IAdminMediaReadService mediaLibrary,
    IContentService content,
    IWebHostEnvironment environment) : PublicController
{
    [HttpGet("{lang:culture}/partners/dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var model = await dashboard.GetForUserAsync(UserId(), cancellationToken);
        if (model is null)
        {
            return Forbid();
        }

        SetMeta(Meta(
            relativePath: "partners/dashboard",
            title: $"Partner dashboard — {BrandName}",
            description: "Your referrals, commissions and payouts.",
            noIndex: true));

        return View(new PartnerPortalViewModel(model, Error: null, Saved: false));
    }

    [HttpPost("{lang:culture}/partners/dashboard/payout-details")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePayoutDetails(PartnerPayoutDetailsForm form, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(form);
        var result = await dashboard.UpdatePayoutDetailsAsync(
            UserId(), form.Iban, form.AccountHolder, form.TaxId, form.Country, cancellationToken);

        var model = await dashboard.GetForUserAsync(UserId(), cancellationToken);
        if (model is null)
        {
            return Forbid();
        }

        SetMeta(Meta(
            relativePath: "partners/dashboard",
            title: $"Partner dashboard — {BrandName}",
            description: "Your referrals, commissions and payouts.",
            noIndex: true));
        return View("Dashboard", new PartnerPortalViewModel(model, result.Ok ? null : result.Error, result.Ok));
    }

    [HttpGet("{lang:culture}/partners/dashboard/statement/{payoutId:guid}")]
    public async Task<IActionResult> Statement(Guid payoutId, CancellationToken cancellationToken)
    {
        // Only the partner's own payouts resolve — the id must appear in their payout list.
        var payouts = await dashboard.GetPayoutsAsync(UserId(), cancellationToken);
        if (payouts is null || payouts.All(p => p.PayoutId != payoutId))
        {
            return NotFound();
        }

        var pdf = await statements.GenerateAsync(payoutId, cancellationToken);
        if (pdf is null)
        {
            return NotFound();
        }

        var period = payouts.First(p => p.PayoutId == payoutId).PeriodLabel;
        return File(pdf, "application/pdf", $"gaia-skyline-payout-{period}.pdf");
    }

    /// <summary>
    /// The media kit: public-ready library images plus the partner copy blocks in all five languages, as a
    /// ZIP built on the fly (nothing partner-specific inside — their code is on the dashboard).
    /// </summary>
    [HttpGet("{lang:culture}/partners/dashboard/media-kit.zip")]
    public async Task<IActionResult> MediaKit(CancellationToken cancellationToken)
    {
        if (await dashboard.GetForUserAsync(UserId(), cancellationToken) is null)
        {
            return Forbid();
        }

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Approved photos = the library images already cleared for public use (alt in every language).
            var images = await mediaLibrary.GetLibraryAsync(MediaKind.Image, includeDeleted: false, cancellationToken);
            foreach (var image in images.Where(i => i.ReadyForPublic))
            {
                var relative = image.BlobUri.TrimStart('/');
                var path = Path.Combine(environment.WebRootPath, relative.Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(path))
                {
                    zip.CreateEntryFromFile(path, $"photos/{Path.GetFileName(path)}", CompressionLevel.Optimal);
                }
            }

            // Copy blocks per language: the landing headline + intro the partner may quote.
            foreach (var culture in SupportedCultures.All)
            {
                var payload = await content.GetSectionAsync("partners", culture.Culture, cancellationToken);
                var text = payload.TextOr("partners.landing.headline", string.Empty)
                    + Environment.NewLine + Environment.NewLine
                    + StripTags(payload.TextOr("partners.landing.intro", string.Empty));
                var entry = zip.CreateEntry($"copy/{culture.Slug}.txt", CompressionLevel.Optimal);
                await using var stream = entry.Open();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(text);
            }
        }

        return File(buffer.ToArray(), "application/zip", "gaia-skyline-media-kit.zip");
    }

    private Guid UserId() =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id
            : Guid.Empty;

    private static string StripTags(string html)
    {
        var builder = new System.Text.StringBuilder(html.Length);
        var inTag = false;
        foreach (var c in html)
        {
            if (c == '<')
            {
                inTag = true;
            }
            else if (c == '>')
            {
                inTag = false;
            }
            else if (!inTag)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Trim();
    }
}
