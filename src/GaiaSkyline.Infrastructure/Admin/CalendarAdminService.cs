using System.Globalization;
using GaiaSkyline.Application.Admin;
using GaiaSkyline.Application.Availability;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Admin;

/// <summary>
/// Builds the admin calendar (Stage 7 §7): every booking, imported iCal block and owner block in a
/// month/week grid with identity (for edit/delete links) and the ICS-export hint per item. Also owns
/// the conflict-resolution write. Untracked reads; mirrors the dashboard's colour semantics.
/// </summary>
internal sealed class CalendarAdminService(
    AppDbContext dbContext,
    IOwnerBlockService ownerBlocks,
    TimeProvider clock) : ICalendarAdminService
{
    /// <summary>Cancelled/refund-only bookings release their nights; everything else occupies.</summary>
    private static readonly BookingStatus[] OccupyingStatuses =
        [BookingStatus.AwaitingPayment, BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Completed];

    public Task<CalendarPeriod> GetMonthAsync(DateOnly anchor, CancellationToken cancellationToken)
    {
        var monthStart = new DateOnly(anchor.Year, anchor.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var gridStart = MondayOnOrBefore(monthStart);
        var gridEnd = monthEnd.DayOfWeek == DayOfWeek.Monday ? monthEnd : MondayOnOrBefore(monthEnd).AddDays(7);
        return BuildAsync(gridStart, gridEnd, monthStart, monthEnd, cancellationToken);
    }

    public Task<CalendarPeriod> GetWeekAsync(DateOnly anchor, CancellationToken cancellationToken)
    {
        var weekStart = MondayOnOrBefore(anchor);
        var weekEnd = weekStart.AddDays(7);
        return BuildAsync(weekStart, weekEnd, weekStart, weekEnd, cancellationToken);
    }

    public async Task<bool> ResolveConflictAsync(Guid id, string? note, CancellationToken cancellationToken)
    {
        var conflict = await dbContext.BookingConflicts
            .FirstOrDefaultAsync(c => c.Id == Domain.Identifiers.BookingConflictId.From(id), cancellationToken);
        if (conflict is null)
        {
            return false;
        }

        conflict.Resolve(clock.GetUtcNow().UtcDateTime, note);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<CalendarPeriod> BuildAsync(
        DateOnly gridStart, DateOnly gridEnd, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken)
    {
        var items = new List<CalendarItem>();

        var bookings = await dbContext.Bookings.AsNoTracking()
            .Where(b => OccupyingStatuses.Contains(b.Status) && b.CheckIn < gridEnd && gridStart < b.CheckOut)
            .Select(b => new { b.Id, b.ReferenceCode, b.GuestName, b.CheckIn, b.CheckOut })
            .ToListAsync(cancellationToken);
        items.AddRange(bookings.Select(b => new CalendarItem(
            CalendarItemKind.DirectBooking, $"{b.ReferenceCode} · {b.GuestName}", b.CheckIn, b.CheckOut,
            InIcsExport: true, BookingId: b.Id.Value)));

        // Imported blocks store an inclusive last night; the calendar uses exclusive ends throughout.
        var imported = await dbContext.ExternalCalendarBlocks.AsNoTracking()
            .Where(b => b.IsActive && b.StartDate < gridEnd && gridStart <= b.EndDate)
            .Select(b => new { b.Source, b.StartDate, b.EndDate, b.Summary })
            .ToListAsync(cancellationToken);
        items.AddRange(imported.Select(b => new CalendarItem(
            CalendarItemKind.ImportedBlock, $"{b.Source}{(string.IsNullOrEmpty(b.Summary) ? "" : $" · {b.Summary}")}",
            b.StartDate, b.EndDate.AddDays(1), InIcsExport: false)));

        var blocks = await ownerBlocks.ListAsync(gridStart, gridEnd, cancellationToken);
        items.AddRange(blocks.Select(b => new CalendarItem(
            b.Kind == OwnerBlockKind.ExternalBooking ? CalendarItemKind.ExternalOwnerBlock : CalendarItemKind.OwnerUnavailable,
            b.Kind == OwnerBlockKind.ExternalBooking ? "External booking" : "Owner unavailable",
            b.StartDate, b.EndDate,
            InIcsExport: b.Kind == OwnerBlockKind.OwnerUnavailable,
            OwnerBlockId: b.Id, Note: b.Note)));

        var cells = new List<CalendarDayCell>();
        for (var date = gridStart; date < gridEnd; date = date.AddDays(1))
        {
            var day = date;
            var onDay = items
                .Where(i => day >= i.StartDate && day < i.EndDateExclusive)
                .OrderBy(i => i.Kind)
                .ToList();
            cells.Add(new CalendarDayCell(day, day >= periodStart && day < periodEnd, onDay));
        }

        var (manualMode, status) = await CalendarStatusAsync(cancellationToken);
        var conflicts = await OpenConflictsAsync(cancellationToken);
        var duplicates = await ownerBlocks.ListImportedDuplicatesAsync(cancellationToken);

        var periodBlocks = items
            .Where(i => i.OwnerBlockId is not null)
            .OrderBy(i => i.StartDate)
            .ToList();

        return new CalendarPeriod(
            gridStart, gridEnd, cells, periodBlocks, manualMode, status, conflicts, duplicates.Count);
    }

    private async Task<IReadOnlyList<OpenConflict>> OpenConflictsAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.BookingConflicts.AsNoTracking()
            .Where(c => c.ResolvedAtUtc == null)
            .OrderByDescending(c => c.DetectedAtUtc)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        // Resolve booking ids for deep links (conflicts store the reference, not the id).
        var references = rows.Select(c => c.BookingReference).Distinct().ToList();
        var ids = await dbContext.Bookings.AsNoTracking()
            .Where(b => references.Contains(b.ReferenceCode))
            .Select(b => new { b.ReferenceCode, b.Id })
            .ToListAsync(cancellationToken);
        var idByReference = ids.ToDictionary(b => b.ReferenceCode, b => b.Id.Value);

        return rows.Select(c => new OpenConflict(
            c.Id.Value, c.BookingReference,
            idByReference.TryGetValue(c.BookingReference, out var bookingId) ? bookingId : null,
            c.SourceName, c.StartDate, c.EndDate, c.DetectedAtUtc)).ToList();
    }

    /// <summary>Same wording as the dashboard's sync panel so the two never disagree.</summary>
    private async Task<(bool Manual, string Status)> CalendarStatusAsync(CancellationToken cancellationToken)
    {
        var sources = await dbContext.ExternalCalendarSources.AsNoTracking()
            .Where(s => s.IsEnabled)
            .Select(s => new { s.Name, s.LastSuccessUtc, s.ConsecutiveFailures })
            .ToListAsync(cancellationToken);

        if (sources.Count == 0)
        {
            return (true, "Manual mode — no external calendars connected. Mirror changes in Hostify yourself.");
        }

        var lastSync = sources.Where(s => s.LastSuccessUtc != null).Select(s => s.LastSuccessUtc!.Value)
            .DefaultIfEmpty().Max();
        var failing = sources.Count(s => s.ConsecutiveFailures >= 3);
        var names = string.Join(", ", sources.Select(s => s.Name));
        var last = lastSync == default ? "never" : lastSync.ToString("u", CultureInfo.InvariantCulture);
        var failText = failing > 0 ? $" · {failing} source(s) failing" : string.Empty;
        return (false, $"Connected: {names} · last sync {last}{failText}");
    }

    private static DateOnly MondayOnOrBefore(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-offset);
    }
}
