using System.Globalization;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Documents;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner bookings manager at /admin/bookings (Stage 7 §6): a filterable list (status, dates, payment
/// method, manual-sync state), manual phone/walk-in booking entry, a detail page with the Stripe dashboard
/// link, the guest document (confirmation/receipt), audit trail and notes, resend actions, and the legal
/// status actions — cancel takes a
/// policy-prefilled, editable refund. Every action honors the domain status machine, is audited and toasts.
/// </summary>
[Route("admin/bookings")]
public sealed class BookingsAdminController(
    IAdminBookingReadService read,
    IAdminBookingService bookings,
    IAuditLog audit,
    IAuditReadStore auditRead,
    IQuoteService quotes,
    IGuestDocumentService guestDocuments,
    IEmailJobScheduler emailScheduler) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? status, string? q, DateOnly? from, DateOnly? to, string? payment, string? synced,
        CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Bookings";
        BookingStatus? statusFilter = Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var parsed) ? parsed : null;
        bool? syncedFilter = synced switch { "yes" => true, "no" => false, _ => null };
        var filter = new BookingAdminFilter(statusFilter, q, from, to, string.IsNullOrWhiteSpace(payment) ? null : payment, syncedFilter);
        var items = await read.GetAsync(filter, cancellationToken);
        return View(new BookingsAdminIndexViewModel(items, statusFilter?.ToString(), q, from, to, payment, synced));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var booking = await read.GetDetailAsync(id, cancellationToken);
        if (booking is null)
        {
            return NotFound();
        }

        // A booking is audited under both keys: admin actions use the id, guest actions the reference.
        var trail = await auditRead.QueryAsync(
            new AuditLogQuery(EntityType: "Booking", PageSize: 20, EntityIds: [id.ToString(), booking.ReferenceCode]),
            cancellationToken);

        ViewData["Title"] = $"Booking {booking.ReferenceCode}";
        return View(new BookingAdminDetailViewModel(booking, trail.Items));
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        ViewData["Title"] = "New manual booking";
        return View(new ManualBookingViewModel(new ManualBookingForm(), null, null));
    }

    [HttpPost("new/preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(ManualBookingForm form, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "New manual booking";
        if (form.CheckIn is not { } checkIn || form.CheckOut is not { } checkOut || checkOut <= checkIn)
        {
            return View("New", new ManualBookingViewModel(form, null, "Pick a check-in and a later check-out to preview the price."));
        }

        var quote = await quotes.QuoteAsync(
            new QuoteRequest(checkIn, checkOut, new GuestParty(form.Adults, form.Children, form.Infants)),
            cancellationToken);
        return View("New", new ManualBookingViewModel(form, quote, null));
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ManualBookingForm form, string? amountReceivedText, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "New manual booking";
        if (!TryParseMoney(amountReceivedText, out var amountReceived))
        {
            return View("New", new ManualBookingViewModel(form, null, "The agreed amount is not a valid number."));
        }

        if (!ModelState.IsValid || form.CheckIn is not { } checkIn || form.CheckOut is not { } checkOut)
        {
            return View("New", new ManualBookingViewModel(form, null, "Fill in the required fields."));
        }

        var result = await bookings.CreateManualAsync(
            new ManualBookingCommand(
                checkIn, checkOut, form.Adults, form.Children, form.Infants,
                form.GuestName, form.GuestEmail, form.GuestPhone, form.GuestCountry.ToUpperInvariant(),
                form.GuestLanguage, form.PaymentMethod, amountReceived, form.SpecialRequests, form.Notes,
                form.SendGuestConfirmation, form.PromoCode),
            cancellationToken);

        if (!result.Ok || result.BookingId is not { } bookingId)
        {
            return View("New", new ManualBookingViewModel(form, null, result.Error ?? "Could not create the booking."));
        }

        await audit.WriteAsync(
            "booking.manual-create", ActorId, Ip, "Booking", bookingId.ToString(),
            new { form.PaymentMethod, Amount = amountReceived }, cancellationToken);
        Toast("Manual booking created and confirmed.");
        return LocalRedirect($"/admin/bookings/{bookingId}");
    }

    [HttpPost("{id:guid}/check-in")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CheckIn(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => bookings.CheckInAsync(id, cancellationToken), "booking.checkin", "Guest checked in.", cancellationToken);

    [HttpPost("{id:guid}/complete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) =>
        ActAsync(id, () => bookings.CompleteAsync(id, cancellationToken), "booking.complete", "Booking marked completed.", cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, string? reason, string? refundAmount, CancellationToken cancellationToken)
    {
        if (!TryParseMoney(refundAmount, out var refund))
        {
            Toast("The refund amount is not a valid number.", "error");
            return LocalRedirect($"/admin/bookings/{id}");
        }

        return await ActAsync(
            id, () => bookings.CancelAsync(id, reason, refund ?? 0m, cancellationToken), "booking.cancel",
            refund > 0 ? $"Booking cancelled, dates released and €{refund:0.00} refund issued." : "Booking cancelled and dates released.",
            cancellationToken, new { reason, refund });
    }

    [HttpPost("{id:guid}/refund")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Refund(Guid id, bool partial, string? amount, CancellationToken cancellationToken)
    {
        if (!TryParseMoney(amount, out var amountEur) || (partial && amountEur is null))
        {
            Toast("Enter a valid refund amount.", "error");
            return LocalRedirect($"/admin/bookings/{id}");
        }

        return await ActAsync(
            id, () => bookings.RefundAsync(id, partial, partial ? amountEur : null, cancellationToken), "booking.refund",
            partial ? $"Partial refund of €{amountEur:0.00} recorded." : "Full refund recorded.",
            cancellationToken, new { partial, amountEur });
    }

    [HttpPost("{id:guid}/resend-confirmation")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResendConfirmation(Guid id, CancellationToken cancellationToken) =>
        ResendAsync(id, BookingEmailKind.Confirmation, "booking.resend-confirmation", "Confirmation email resent to the guest.", cancellationToken);

    [HttpPost("{id:guid}/resend-pm")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResendPropertyManager(Guid id, CancellationToken cancellationToken) =>
        ResendAsync(id, BookingEmailKind.PropertyManager, "booking.resend-pm", "Property-manager email resent.", cancellationToken);

    [HttpGet("{id:guid}/document.pdf")]
    public async Task<IActionResult> Document(Guid id, CancellationToken cancellationToken)
    {
        var booking = await read.GetDetailAsync(id, cancellationToken);
        if (booking is null)
        {
            return NotFound();
        }

        var (type, name) = booking.Status is BookingStatus.Cancelled or BookingStatus.Refunded or BookingStatus.PartiallyRefunded
            ? (GuestDocumentType.CancellationRefund, "cancellation")
            : (GuestDocumentType.Confirmation, "confirmation");

        var pdf = await guestDocuments.GenerateAsync(booking.ReferenceCode, type, cancellationToken);
        return pdf is null
            ? NotFound()
            : File(pdf, "application/pdf", $"gaia-skyline-{name}-{booking.ReferenceCode}.pdf");
    }

    [HttpPost("{id:guid}/notes")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Notes(Guid id, string? notes, CancellationToken cancellationToken) =>
        ActAsync(id, () => bookings.SetNotesAsync(id, notes, cancellationToken), "booking.notes", "Notes saved.", cancellationToken);

    [HttpPost("{id:guid}/sync")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Sync(Guid id, string? note, CancellationToken cancellationToken) =>
        ActAsync(id, () => bookings.MarkSyncedAsync(id, note, cancellationToken), "booking.sync", "Marked synced in the management system.", cancellationToken);

    private async Task<IActionResult> ResendAsync(
        Guid id, BookingEmailKind kind, string auditEvent, string success, CancellationToken cancellationToken)
    {
        var booking = await read.GetDetailAsync(id, cancellationToken);
        if (booking is null)
        {
            return NotFound();
        }

        if (booking.ConfirmedAtUtc is null)
        {
            Toast("This booking was never confirmed — there is no confirmation to resend.", "error");
            return LocalRedirect($"/admin/bookings/{id}");
        }

        await emailScheduler.EnqueueAsync(id, kind, cancellationToken);
        await audit.WriteAsync(auditEvent, ActorId, Ip, "Booking", id.ToString(), null, cancellationToken);
        Toast(success);
        return LocalRedirect($"/admin/bookings/{id}");
    }

    private async Task<IActionResult> ActAsync(
        Guid id, Func<Task<BookingActionResult>> action, string auditEvent, string success,
        CancellationToken cancellationToken, object? details = null)
    {
        var result = await action();
        if (!result.Ok)
        {
            Toast(result.Error ?? "Could not update the booking.", "error");
            return LocalRedirect($"/admin/bookings/{id}");
        }

        await audit.WriteAsync(auditEvent, ActorId, Ip, "Booking", id.ToString(), details, cancellationToken);
        Toast(success);
        return LocalRedirect($"/admin/bookings/{id}");
    }

    /// <summary>
    /// Parses a money input tolerantly ("150", "150.50", "150,50", "1 234,56") — admin requests carry the
    /// browser's Accept-Language culture, so a fixed-culture bind would reject half the inputs.
    /// </summary>
    private static bool TryParseMoney(string? text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var s = text.Replace(" ", "", StringComparison.Ordinal).Replace("€", "", StringComparison.Ordinal);
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            // Both present: the later one is the decimal separator, the other is thousands.
            var (dec, thou) = lastComma > lastDot ? (',', '.') : ('.', ',');
            s = s.Replace(thou.ToString(), "", StringComparison.Ordinal).Replace(dec, '.');
        }
        else if (lastComma >= 0)
        {
            s = s.Replace(',', '.');
        }

        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            return false;
        }

        value = parsed;
        return true;
    }
}
