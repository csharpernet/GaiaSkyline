using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner payments monitor at /admin/payments (Stage 7 §9): live Stripe status (mode, enabled
/// methods, last webhook), the StripeEventLog browser with filters and re-process for failed events,
/// refund history, open disputes with evidence deadlines and dashboard links, and the Multibanco
/// monitor with a typed manual cancel.
/// </summary>
[Route("admin/payments")]
public sealed class PaymentsAdminController(
    IPaymentsAdminReadService read,
    IStripeWebhookHandler webhooks,
    IAdminBookingService bookings,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? type, bool failed, int page = 1, CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Payments";
        var model = new PaymentsAdminViewModel(
            await read.GetStripeStatusAsync(cancellationToken),
            await read.GetEventsAsync(new StripeEventFilter(type, failed, page), cancellationToken),
            await read.GetRefundHistoryAsync(cancellationToken),
            await read.GetOpenDisputesAsync(cancellationToken),
            await read.GetMultibancoPendingAsync(cancellationToken),
            type, failed);
        return View(model);
    }

    [HttpPost("events/{id:guid}/reprocess")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reprocess(Guid id, CancellationToken cancellationToken)
    {
        var result = await webhooks.ReprocessAsync(id, cancellationToken);
        await audit.WriteAsync("payments.event.reprocess", ActorId, Ip, "StripeEventLog", id.ToString(),
            new { outcome = result.Outcome.ToString(), result.Message }, cancellationToken);
        if (result.Outcome == WebhookOutcome.Ok)
        {
            Toast("Event re-processed.");
        }
        else
        {
            Toast(result.Message ?? "Re-processing failed — the error was recorded on the event.", "error");
        }

        return LocalRedirect("/admin/payments?failed=true");
    }

    [HttpPost("multibanco/{id:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelMultibanco(Guid id, CancellationToken cancellationToken)
    {
        // A Multibanco hold was never charged, so this is a plain cancel (dates released, guest + PM told).
        var result = await bookings.CancelAsync(id, "Multibanco voucher cancelled by the owner", 0m, cancellationToken);
        if (!result.Ok)
        {
            Toast(result.Error ?? "Could not cancel the hold.", "error");
            return LocalRedirect("/admin/payments");
        }

        await audit.WriteAsync("payments.multibanco.cancel", ActorId, Ip, "Booking", id.ToString(), null, cancellationToken);
        Toast("Multibanco hold cancelled and its dates released.");
        return LocalRedirect("/admin/payments");
    }
}
