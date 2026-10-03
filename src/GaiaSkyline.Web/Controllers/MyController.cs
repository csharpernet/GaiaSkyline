using System.Globalization;
using System.Text;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The guest area (noindex, no-store). Access to a booking is granted either to a signed-in guest whose
/// email owns it, or to a visitor who consumed a magic link (booking-scoped cookie). The bookings list
/// requires a signed-in account.
/// </summary>
[Route("{lang:culture}/my")]
public sealed class MyController(
    IBookingReadStore readStore,
    IPricingReadStore pricingReadStore,
    IRefundService refundService,
    IBookingLifecycleService lifecycleService,
    IInvoiceService invoiceService,
    IContentService content,
    BookingAccessCookie bookingAccess,
    IAuditLog audit,
    TimeProvider clock) : PublicController
{
    private string Ip => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    [HttpGet("bookings")]
    [Authorize]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Bookings(CancellationToken cancellationToken)
    {
        SetMeta(Meta("my/bookings", $"Your bookings — {BrandName}", "Your Gaia Skyline bookings.", noIndex: true));
        var email = User.Identity?.Name;
        if (string.IsNullOrEmpty(email))
        {
            return LocalRedirect($"/{CurrentSlug}/account/login");
        }

        var bookings = await readStore.GetForGuestEmailAsync(email, cancellationToken);
        return View(bookings);
    }

    [HttpGet("booking/{reference}")]
    [AllowAnonymous]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Booking(string reference, CancellationToken cancellationToken)
    {
        var dto = await readStore.GetByReferenceAsync(reference, cancellationToken);
        if (dto is null || !CanAccess(dto))
        {
            return NotFound();
        }

        SetMeta(Meta($"my/booking/{reference}", $"Booking {reference} — {BrandName}", "Your booking.", noIndex: true));

        var (pct, amount) = await ComputeRefundAsync(dto, cancellationToken);
        var daysUntil = dto.CheckIn.DayNumber - LisbonClock.Today(clock).DayNumber;
        var showCheckin = daysUntil is >= 0 and <= 7
            && dto.Status is BookingStatus.Confirmed or BookingStatus.CheckedIn;
        var checkin = await content.GetSectionAsync("checkin", CurrentCulture, cancellationToken);

        return View(new MyBookingViewModel(dto, pct, amount, CanCancel(dto), showCheckin, checkin));
    }

    [HttpGet("booking/{reference}/invoice.pdf")]
    [AllowAnonymous]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Invoice(string reference, CancellationToken cancellationToken)
    {
        var dto = await readStore.GetByReferenceAsync(reference, cancellationToken);
        if (dto is null || !CanAccess(dto))
        {
            return NotFound();
        }

        var pdf = await invoiceService.GenerateAsync(reference, cancellationToken);
        return pdf is null
            ? NotFound()
            : File(pdf, "application/pdf", $"gaia-skyline-invoice-{reference}.pdf");
    }

    [HttpGet("booking/{reference}/calendar.ics")]
    [AllowAnonymous]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Calendar(string reference, CancellationToken cancellationToken)
    {
        var dto = await readStore.GetByReferenceAsync(reference, cancellationToken);
        if (dto is null || !CanAccess(dto))
        {
            return NotFound();
        }

        return File(Encoding.UTF8.GetBytes(BuildIcs(dto)), "text/calendar; charset=utf-8", $"gaia-skyline-{reference}.ics");
    }

    [HttpPost("booking/{reference}/cancel")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [OutputCache(NoStore = true)]
    public async Task<IActionResult> Cancel(string reference, CancellationToken cancellationToken)
    {
        var dto = await readStore.GetByReferenceAsync(reference, cancellationToken);
        if (dto is null || !CanAccess(dto))
        {
            return NotFound();
        }

        if (CanCancel(dto))
        {
            var (pct, amount) = await ComputeRefundAsync(dto, cancellationToken);
            var bookingId = BookingId.From(dto.Id);

            // Confirmed/paid bookings get the policy refund; the charge.refunded webhook then moves the
            // booking to Refunded/PartiallyRefunded. The cancel itself frees the dates immediately.
            if (amount > 0 && dto.Status is BookingStatus.Confirmed)
            {
                await refundService.RefundAsync(bookingId, amount, "guest_cancellation", cancellationToken);
            }

            await lifecycleService.CancelAndReleaseAsync(bookingId, "Guest cancellation", cancellationToken);
            await audit.WriteAsync("booking.cancelled", null, Ip, "Booking", reference, new { pct, amount }, cancellationToken);
        }

        return LocalRedirect($"/{CurrentSlug}/my/booking/{reference}");
    }

    private bool CanAccess(BookingSummaryDto booking) =>
        (User.Identity?.IsAuthenticated == true
            && string.Equals(User.Identity.Name, booking.GuestEmail, StringComparison.OrdinalIgnoreCase))
        || bookingAccess.HasAccess(Request, booking.ReferenceCode);

    private static bool CanCancel(BookingSummaryDto booking) =>
        booking.Status is BookingStatus.AwaitingPayment or BookingStatus.Confirmed;

    private async Task<(int Pct, decimal Amount)> ComputeRefundAsync(BookingSummaryDto booking, CancellationToken cancellationToken)
    {
        var policy = await pricingReadStore.GetCancellationPolicyAsync(cancellationToken);
        var days = booking.CheckIn.DayNumber - LisbonClock.Today(clock).DayNumber;
        var pct = policy?.RefundPercentageFor(days) ?? 0;
        var amount = Math.Round(booking.Total * pct / 100m, 2, MidpointRounding.AwayFromZero);
        return (pct, amount);
    }

    private static string BuildIcs(BookingSummaryDto booking)
    {
        static string D(DateOnly d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return string.Join("\r\n",
            "BEGIN:VCALENDAR",
            "VERSION:2.0",
            "PRODID:-//GaiaSkyline//v1//EN",
            "CALSCALE:GREGORIAN",
            "METHOD:PUBLISH",
            "BEGIN:VEVENT",
            $"UID:booking-{booking.ReferenceCode}@gaiaskyline",
            $"DTSTART;VALUE=DATE:{D(booking.CheckIn)}",
            $"DTEND;VALUE=DATE:{D(booking.CheckOut)}",
            "SUMMARY:Gaia Skyline — your stay",
            "STATUS:CONFIRMED",
            "END:VEVENT",
            "END:VCALENDAR",
            string.Empty);
    }
}
