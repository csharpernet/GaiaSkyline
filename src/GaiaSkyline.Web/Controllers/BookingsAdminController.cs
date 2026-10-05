using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner bookings manager at /admin/bookings (Stage 7 §6): a filterable list, a read-only detail with the
/// legal status actions (check-in, complete, cancel, refund), an internal-notes editor and a "synced in the
/// management system" acknowledgement. Every action honors the domain status machine, is audited and toasts.
/// </summary>
[Route("admin/bookings")]
public sealed class BookingsAdminController(
    IAdminBookingReadService read,
    IAdminBookingService bookings,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? status, string? q, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Bookings";
        BookingStatus? statusFilter = Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var parsed) ? parsed : null;
        var filter = new BookingAdminFilter(statusFilter, q, from, to);
        var items = await read.GetAsync(filter, cancellationToken);
        return View(new BookingsAdminIndexViewModel(items, statusFilter?.ToString(), q, from, to));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var booking = await read.GetDetailAsync(id, cancellationToken);
        if (booking is null)
        {
            return NotFound();
        }

        ViewData["Title"] = $"Booking {booking.ReferenceCode}";
        return View(booking);
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
    public Task<IActionResult> Cancel(Guid id, string? reason, CancellationToken cancellationToken) =>
        ActAsync(id, () => bookings.CancelAsync(id, reason, cancellationToken), "booking.cancel", "Booking cancelled and dates released.", cancellationToken);

    [HttpPost("{id:guid}/refund")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Refund(Guid id, bool partial, CancellationToken cancellationToken) =>
        ActAsync(id, () => bookings.RefundAsync(id, partial, cancellationToken), "booking.refund",
            partial ? "Booking marked partially refunded." : "Booking marked refunded.", cancellationToken);

    [HttpPost("{id:guid}/notes")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Notes(Guid id, string? notes, CancellationToken cancellationToken) =>
        ActAsync(id, () => bookings.SetNotesAsync(id, notes, cancellationToken), "booking.notes", "Notes saved.", cancellationToken);

    [HttpPost("{id:guid}/sync")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Sync(Guid id, string? note, CancellationToken cancellationToken) =>
        ActAsync(id, () => bookings.MarkSyncedAsync(id, note, cancellationToken), "booking.sync", "Marked synced in the management system.", cancellationToken);

    private async Task<IActionResult> ActAsync(
        Guid id, Func<Task<BookingActionResult>> action, string auditEvent, string success, CancellationToken cancellationToken)
    {
        var result = await action();
        if (!result.Ok)
        {
            Toast(result.Error ?? "Could not update the booking.", "error");
            return LocalRedirect($"/admin/bookings/{id}");
        }

        await audit.WriteAsync(auditEvent, ActorId, Ip, "Booking", id.ToString(), null, cancellationToken);
        Toast(success);
        return LocalRedirect($"/admin/bookings/{id}");
    }
}
