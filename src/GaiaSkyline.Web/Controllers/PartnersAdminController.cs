using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Partners;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// Partner-application review at /admin/partners (Stage 7 §11): pending applications first, a single
/// approve/reject decision each (with an optional note). The partner program itself ships in Stage 8.
/// </summary>
[Route("admin/partners")]
public sealed class PartnersAdminController(
    IPartnerApplicationsAdminService applications,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Partners";
        return View(await applications.GetAllAsync(cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(Guid id, string? note, CancellationToken cancellationToken) =>
        DecideAsync(id, approve: true, note, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(Guid id, string? note, CancellationToken cancellationToken) =>
        DecideAsync(id, approve: false, note, cancellationToken);

    private async Task<IActionResult> DecideAsync(Guid id, bool approve, string? note, CancellationToken cancellationToken)
    {
        var result = approve
            ? await applications.ApproveAsync(id, note, cancellationToken)
            : await applications.RejectAsync(id, note, cancellationToken);
        if (!result.Ok)
        {
            Toast(result.Error ?? "The decision could not be saved.", "error");
            return LocalRedirect("/admin/partners");
        }

        await audit.WriteAsync(
            approve ? "partner_application.approve" : "partner_application.reject",
            ActorId, Ip, "PartnerApplication", id.ToString(), new { note }, cancellationToken);
        Toast(approve ? "Application approved." : "Application rejected.");
        return LocalRedirect("/admin/partners");
    }
}
