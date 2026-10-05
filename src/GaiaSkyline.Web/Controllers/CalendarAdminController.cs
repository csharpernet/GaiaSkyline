using GaiaSkyline.Application.Admin;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Availability;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner calendar at /admin/calendar (Stage 7 §7): month/week views of every booking, imported
/// iCal block and owner block (dashboard colours), quick add/edit/delete of owner blocks with the
/// domain's booking-overlap error, per-item ICS-export hints, a force re-import action (disabled in
/// manual mode), the imported-duplicate cleanup view and conflict resolution.
/// </summary>
[Route("admin/calendar")]
public sealed class CalendarAdminController(
    ICalendarAdminService calendar,
    IOwnerBlockService ownerBlocks,
    IExternalCalendarImporter importer,
    IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? view, DateOnly? anchor, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Calendar";
        var mode = view == "week" ? "week" : "month";
        var at = anchor ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var period = mode == "week"
            ? await calendar.GetWeekAsync(at, cancellationToken)
            : await calendar.GetMonthAsync(at, cancellationToken);
        var (prev, next) = mode == "week"
            ? (at.AddDays(-7), at.AddDays(7))
            : (at.AddMonths(-1), at.AddMonths(1));
        return View(new CalendarAdminViewModel(period, mode, at, prev, next));
    }

    [HttpPost("blocks")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBlock(
        DateOnly? start, DateOnly? end, string kind, string? note, string? view, DateOnly? anchor, CancellationToken cancellationToken)
    {
        if (start is not { } startDate || end is not { } endDate || endDate <= startDate)
        {
            Toast("Pick a start date and a later end date (the first free night).", "error");
            return Back(view, anchor);
        }

        var blockKind = kind == "owner-unavailable" ? OwnerBlockKind.OwnerUnavailable : OwnerBlockKind.ExternalBooking;
        try
        {
            var id = await ownerBlocks.CreateAsync(startDate, endDate, blockKind, note, ActorName, cancellationToken);
            await audit.WriteAsync("owner_block.create", ActorId, Ip, "OwnerBlock", id.ToString(),
                new { startDate, endDate, kind = blockKind.ToString(), note }, cancellationToken);
            Toast($"{(blockKind == OwnerBlockKind.OwnerUnavailable ? "Owner-unavailable" : "External-booking")} block added.");
        }
        catch (OwnerBlockConflictsWithBookingException ex)
        {
            Toast(ex.Message, "error");
        }

        return Back(view, anchor ?? start);
    }

    [HttpPost("blocks/{id:guid}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBlock(
        Guid id, DateOnly? start, DateOnly? end, string? note, string? view, DateOnly? anchor, CancellationToken cancellationToken)
    {
        if (start is not { } startDate || end is not { } endDate || endDate <= startDate)
        {
            Toast("Pick a start date and a later end date (the first free night).", "error");
            return Back(view, anchor);
        }

        try
        {
            if (!await ownerBlocks.UpdateAsync(id, startDate, endDate, note, cancellationToken))
            {
                Toast("That block no longer exists.", "error");
                return Back(view, anchor);
            }

            await audit.WriteAsync("owner_block.update", ActorId, Ip, "OwnerBlock", id.ToString(),
                new { startDate, endDate, note }, cancellationToken);
            Toast("Block updated.");
        }
        catch (OwnerBlockConflictsWithBookingException ex)
        {
            Toast(ex.Message, "error");
        }

        return Back(view, anchor ?? start);
    }

    [HttpPost("blocks/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBlock(Guid id, string? view, DateOnly? anchor, CancellationToken cancellationToken)
    {
        if (!await ownerBlocks.DeleteAsync(id, cancellationToken))
        {
            Toast("That block no longer exists.", "error");
            return Back(view, anchor);
        }

        await audit.WriteAsync("owner_block.delete", ActorId, Ip, "OwnerBlock", id.ToString(), null, cancellationToken);
        Toast("Block removed.");
        return Back(view, anchor);
    }

    [HttpPost("reimport")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForceReimport(string? view, DateOnly? anchor, CancellationToken cancellationToken)
    {
        await importer.ImportAllAsync(cancellationToken);
        await audit.WriteAsync("calendar.force_reimport", ActorId, Ip, "ExternalCalendarSource", null, null, cancellationToken);
        Toast("iCal re-import finished.");
        return Back(view, anchor);
    }

    [HttpGet("duplicates")]
    public async Task<IActionResult> Duplicates(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Duplicate blocks";
        var duplicates = await ownerBlocks.ListImportedDuplicatesAsync(cancellationToken);
        return View(new CalendarDuplicatesViewModel(duplicates));
    }

    [HttpPost("duplicates/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDuplicates(Guid[] ids, CancellationToken cancellationToken)
    {
        var deleted = 0;
        foreach (var id in ids ?? [])
        {
            if (await ownerBlocks.DeleteAsync(id, cancellationToken))
            {
                deleted++;
                await audit.WriteAsync("owner_block.delete", ActorId, Ip, "OwnerBlock", id.ToString(),
                    new { reason = "imported-duplicate" }, cancellationToken);
            }
        }

        Toast(deleted == 0 ? "Nothing selected." : $"Deleted {deleted} duplicate block(s).", deleted == 0 ? "error" : "success");
        return LocalRedirect("/admin/calendar/duplicates");
    }

    [HttpPost("conflicts/{id:guid}/resolve")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveConflict(Guid id, string? note, string? view, DateOnly? anchor, CancellationToken cancellationToken)
    {
        if (!await calendar.ResolveConflictAsync(id, note, cancellationToken))
        {
            Toast("That conflict no longer exists.", "error");
            return Back(view, anchor);
        }

        await audit.WriteAsync("calendar.conflict_resolve", ActorId, Ip, "BookingConflict", id.ToString(),
            new { note }, cancellationToken);
        Toast("Conflict marked resolved.");
        return Back(view, anchor);
    }

    private LocalRedirectResult Back(string? view, DateOnly? anchor)
    {
        var mode = view == "week" ? "week" : "month";
        var at = anchor?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return LocalRedirect(at is null ? $"/admin/calendar?view={mode}" : $"/admin/calendar?view={mode}&anchor={at}");
    }
}
