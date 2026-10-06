using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Partners;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>The /admin/partners landing: pending applications plus the onboarded partner roster.</summary>
public sealed record PartnersAdminIndexViewModel(
    IReadOnlyList<PartnerApplicationDto> Applications,
    IReadOnlyList<PartnerListItemDto> Partners);

/// <summary>
/// The owner's partner management (Stage 7 §11 applications + Stage 8 Part A program): approving an
/// application immediately creates the partner and sends the 7-day invite; partners can be edited
/// (percentages, code), suspended and reactivated; the commissions ledger, payouts (generate on demand,
/// mark settled, download statements) and the clicks report live here too.
/// </summary>
[Route("admin/partners")]
public sealed class PartnersAdminController(
    IPartnerApplicationsAdminService applications,
    IPartnerOnboardingService onboarding,
    IPartnersAdminService partners,
    IPartnerCommissionService commissions,
    IPartnerStatementPdfService statements,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Partners";
        return View(new PartnersAdminIndexViewModel(
            await applications.GetAllAsync(cancellationToken),
            await partners.GetPartnersAsync(cancellationToken)));
    }

    [HttpPost("{id:guid}/approve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, string? note, CancellationToken cancellationToken)
    {
        var result = await applications.ApproveAsync(id, note, cancellationToken);
        if (!result.Ok)
        {
            Toast(result.Error ?? "The decision could not be saved.", "error");
            return LocalRedirect("/admin/partners");
        }

        await audit.WriteAsync(
            "partner_application.approve", ActorId, Ip, "PartnerApplication", id.ToString(), new { note }, cancellationToken);

        // Approval flows straight into onboarding: partner + promo code + the 7-day invite email.
        var invite = await onboarding.InviteAsync(id, cancellationToken);
        if (invite.Ok)
        {
            await audit.WriteAsync(
                "partner.invite", ActorId, Ip, "Partner", invite.PartnerId!.Value.ToString(), null, cancellationToken);
            Toast("Application approved — the invite email is on its way.");
        }
        else
        {
            Toast($"Approved, but the invite failed: {invite.Error}", "error");
        }

        return LocalRedirect("/admin/partners");
    }

    [HttpPost("{id:guid}/reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string? note, CancellationToken cancellationToken)
    {
        var result = await applications.RejectAsync(id, note, cancellationToken);
        if (!result.Ok)
        {
            Toast(result.Error ?? "The decision could not be saved.", "error");
            return LocalRedirect("/admin/partners");
        }

        await audit.WriteAsync(
            "partner_application.reject", ActorId, Ip, "PartnerApplication", id.ToString(), new { note }, cancellationToken);
        Toast("Application rejected.");
        return LocalRedirect("/admin/partners");
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var partner = await partners.GetPartnerAsync(id, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Partner";
        return View(partner);
    }

    [HttpPost("{id:guid}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid id, int guestDiscountPct, int commissionPct, string promoCode, CancellationToken cancellationToken)
    {
        var result = await partners.UpdatePartnerAsync(id, guestDiscountPct, commissionPct, promoCode, cancellationToken);
        if (result.Ok)
        {
            await audit.WriteAsync(
                "partner.update", ActorId, Ip, "Partner", id.ToString(),
                new { guestDiscountPct, commissionPct, promoCode }, cancellationToken);
            Toast("Partner saved.");
        }
        else
        {
            Toast(result.Error ?? "Could not save the partner.", "error");
        }

        return LocalRedirect($"/admin/partners/{id}");
    }

    [HttpPost("{id:guid}/suspend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
    {
        var result = await partners.SuspendAsync(id, cancellationToken);
        Toast(result.Ok ? "Partner suspended — their code and links stop attributing." : result.Error!, result.Ok ? "info" : "error");
        if (result.Ok)
        {
            await audit.WriteAsync("partner.suspend", ActorId, Ip, "Partner", id.ToString(), null, cancellationToken);
        }

        return LocalRedirect($"/admin/partners/{id}");
    }

    [HttpPost("{id:guid}/reactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await partners.ReactivateAsync(id, cancellationToken);
        Toast(result.Ok ? "Partner reactivated." : result.Error!, result.Ok ? "success" : "error");
        if (result.Ok)
        {
            await audit.WriteAsync("partner.reactivate", ActorId, Ip, "Partner", id.ToString(), null, cancellationToken);
        }

        return LocalRedirect($"/admin/partners/{id}");
    }

    [HttpPost("{id:guid}/resend-invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendInvite(Guid id, CancellationToken cancellationToken)
    {
        var result = await onboarding.ResendInviteAsync(id, cancellationToken);
        Toast(result.Ok ? "A fresh invite is on its way." : result.Error!, result.Ok ? "success" : "error");
        if (result.Ok)
        {
            await audit.WriteAsync("partner.invite", ActorId, Ip, "Partner", id.ToString(), null, cancellationToken);
        }

        return LocalRedirect($"/admin/partners/{id}");
    }

    [HttpGet("ledger")]
    public async Task<IActionResult> Ledger(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Commissions";
        return View(await partners.GetLedgerAsync(cancellationToken));
    }

    [HttpGet("payouts")]
    public async Task<IActionResult> Payouts(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Payouts";
        return View(await partners.GetPayoutsAsync(cancellationToken));
    }

    [HttpPost("payouts/run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunPayouts(CancellationToken cancellationToken)
    {
        var created = await commissions.RunPayoutsAsync(cancellationToken);
        await audit.WriteAsync("partner.payouts_run", ActorId, Ip, "Payout", "run", new { created }, cancellationToken);
        Toast(created == 0
            ? "No payouts created — nothing payable above the minimum (or this period already ran)."
            : $"{created} payout(s) created and the statements emailed.");
        return LocalRedirect("/admin/partners/payouts");
    }

    [HttpPost("payouts/{id:guid}/settle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SettlePayout(Guid id, CancellationToken cancellationToken)
    {
        var result = await partners.MarkPayoutSettledAsync(id, cancellationToken);
        Toast(result.Ok ? "Payout marked settled." : result.Error!, result.Ok ? "success" : "error");
        if (result.Ok)
        {
            await audit.WriteAsync("partner.payout_settle", ActorId, Ip, "Payout", id.ToString(), null, cancellationToken);
        }

        return LocalRedirect("/admin/partners/payouts");
    }

    [HttpGet("payouts/{id:guid}/statement.pdf")]
    public async Task<IActionResult> Statement(Guid id, CancellationToken cancellationToken)
    {
        var pdf = await statements.GenerateAsync(id, cancellationToken);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"gaia-skyline-payout-{id:N}.pdf");
    }

    [HttpGet("clicks")]
    public async Task<IActionResult> Clicks(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Partner clicks";
        return View(await partners.GetClicksReportAsync(cancellationToken));
    }
}
