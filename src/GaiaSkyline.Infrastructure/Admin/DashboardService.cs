using System.Globalization;
using GaiaSkyline.Application.Admin;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Availability;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Admin;

/// <summary>
/// Builds the owner dashboard: KPIs (current month), a colour-coded month calendar, the sync-mode panels,
/// the manual Hostify-sync to-do (manual mode only), open conflicts and Multibanco holds. Reads are
/// untracked except the booking updated by <see cref="MarkSyncedAsync"/>.
/// </summary>
internal sealed class DashboardService(
    AppDbContext dbContext,
    IOptions<PricingProviderOptions> pricingOptions,
    IAuditLog audit,
    TimeProvider clock) : IDashboardService
{
    private static readonly BookingStatus[] ActiveStayStatuses =
        [BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Completed];

    public async Task<DashboardSummary> GetAsync(DateOnly month, CancellationToken cancellationToken)
    {
        var today = LisbonClock.Today(clock);
        var displayMonth = new DateOnly(month.Year, month.Month, 1);
        var currentMonth = new DateOnly(today.Year, today.Month, 1);

        var displayOccupancy = await BuildOccupancyAsync(displayMonth, cancellationToken);
        var currentOccupancy = displayMonth == currentMonth
            ? displayOccupancy
            : await BuildOccupancyAsync(currentMonth, cancellationToken);

        var (bookingsThisMonth, revenueMtd) = await MonthBookingStatsAsync(currentMonth, cancellationToken);
        var occupancyPercent = OccupancyPercent(currentMonth, currentOccupancy);
        var nextCheckIn = await NextCheckInAsync(today, cancellationToken);

        var (calManual, calStatus) = await CalendarStatusAsync(cancellationToken);
        var (priceManual, priceStatus) = await PricingStatusAsync(cancellationToken);

        var manualSync = await GetOutstandingManualSyncAsync(cancellationToken);
        var conflicts = await ConflictsAsync(cancellationToken);
        var multibanco = await MultibancoPendingAsync(cancellationToken);

        return new DashboardSummary(
            bookingsThisMonth, occupancyPercent, revenueMtd, nextCheckIn,
            displayMonth, BuildGrid(displayMonth, displayOccupancy),
            calManual, calStatus, priceManual, priceStatus,
            manualSync, conflicts, multibanco);
    }

    public async Task<IReadOnlyList<ManualSyncItem>> GetOutstandingManualSyncAsync(CancellationToken cancellationToken)
    {
        // Once an external calendar is connected, the two-way iCal sync replaces the manual to-do.
        if (await dbContext.ExternalCalendarSources.AnyAsync(s => s.IsEnabled, cancellationToken))
        {
            return [];
        }

        // Confirmed bookings (blocked in Hostify) not yet mirrored; and bookings that were confirmed and
        // then cancelled (so Hostify was blocked and now needs unblocking), not yet mirrored since cancel.
        // A hold cancelled from AwaitingPayment never blocked Hostify, so it is not a to-do.
        var candidates = await dbContext.Bookings.AsNoTracking()
            .Where(b =>
                (b.Status == BookingStatus.Confirmed && b.ConfirmedAtUtc != null
                    && (b.ExternalChannelSyncedAtUtc == null || b.ExternalChannelSyncedAtUtc < b.ConfirmedAtUtc))
                || (b.Status == BookingStatus.Cancelled && b.CancelledAtUtc != null && b.ConfirmedAtUtc != null
                    && (b.ExternalChannelSyncedAtUtc == null || b.ExternalChannelSyncedAtUtc < b.CancelledAtUtc)))
            .Select(b => new { b.ReferenceCode, b.GuestName, b.CheckIn, b.CheckOut, b.Status, b.ConfirmedAtUtc, b.CancelledAtUtc })
            .ToListAsync(cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var items = new List<ManualSyncItem>();
        foreach (var b in candidates)
        {
            var changeAt = b.Status == BookingStatus.Confirmed ? b.ConfirmedAtUtc : b.CancelledAtUtc;
            if (changeAt is null)
            {
                continue;
            }

            var action = b.Status == BookingStatus.Confirmed ? ManualSyncAction.BlockInHostify : ManualSyncAction.UnblockInHostify;
            var overdue = now - changeAt.Value > TimeSpan.FromHours(24);
            items.Add(new ManualSyncItem(b.ReferenceCode, b.GuestName, b.CheckIn, b.CheckOut, action, changeAt.Value, overdue));
        }

        return items.OrderByDescending(i => i.Overdue).ThenBy(i => i.ChangeAtUtc).ToList();
    }

    public async Task MarkSyncedAsync(string reference, string? note, Guid? actorUserId, string? actorIp, CancellationToken cancellationToken)
    {
        var normalized = reference.Trim().ToUpperInvariant();
        var booking = await dbContext.Bookings.FirstOrDefaultAsync(b => b.ReferenceCode == normalized, cancellationToken);
        if (booking is null)
        {
            return;
        }

        booking.MarkExternalChannelSynced(clock.GetUtcNow().UtcDateTime, note);
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("booking.hostify_synced", actorUserId, actorIp, "Booking", normalized, new { note }, cancellationToken);
    }

    private async Task<Dictionary<DateOnly, DayOccupancy>> BuildOccupancyAsync(DateOnly monthStart, CancellationToken cancellationToken)
    {
        var monthEnd = monthStart.AddMonths(1); // exclusive
        var lastDay = monthEnd.AddDays(-1);
        var map = new Dictionary<DateOnly, DayOccupancy>();

        void Mark(DateOnly date, DayOccupancy occ)
        {
            if (date < monthStart || date >= monthEnd)
            {
                return;
            }

            if (!map.TryGetValue(date, out var existing) || occ > existing)
            {
                map[date] = occ;
            }
        }

        var bookedDates = await dbContext.BookingDateOccupancies.AsNoTracking()
            .Where(o => o.Date >= monthStart && o.Date < monthEnd)
            .Select(o => o.Date)
            .ToListAsync(cancellationToken);
        foreach (var date in bookedDates)
        {
            Mark(date, DayOccupancy.DirectBooking);
        }

        var externalBlocks = await dbContext.ExternalCalendarBlocks.AsNoTracking()
            .Where(b => b.IsActive && b.StartDate <= lastDay && b.EndDate >= monthStart)
            .Select(b => new { b.StartDate, b.EndDate })
            .ToListAsync(cancellationToken);
        foreach (var block in externalBlocks)
        {
            for (var d = block.StartDate; d <= block.EndDate; d = d.AddDays(1)) // EndDate inclusive
            {
                Mark(d, DayOccupancy.ImportedBlock);
            }
        }

        var ownerBlocks = await dbContext.OwnerBlocks.AsNoTracking()
            .Where(b => b.StartDate < monthEnd && b.EndDate > monthStart)
            .Select(b => new { b.StartDate, b.EndDate, b.Kind })
            .ToListAsync(cancellationToken);
        foreach (var block in ownerBlocks)
        {
            var occ = block.Kind == OwnerBlockKind.ExternalBooking ? DayOccupancy.ExternalOwnerBlock : DayOccupancy.OwnerUnavailable;
            for (var d = block.StartDate; d < block.EndDate; d = d.AddDays(1)) // EndDate exclusive
            {
                Mark(d, occ);
            }
        }

        return map;
    }

    private static List<CalendarDay> BuildGrid(DateOnly monthStart, Dictionary<DateOnly, DayOccupancy> occupancy)
    {
        var monthEnd = monthStart.AddMonths(1);
        // Monday-first grid covering whole weeks.
        var gridStart = monthStart.AddDays(-((int)monthStart.DayOfWeek + 6) % 7);
        var lastDay = monthEnd.AddDays(-1);
        var gridEnd = lastDay.AddDays((7 - ((int)lastDay.DayOfWeek + 6) % 7 - 1) % 7);

        var days = new List<CalendarDay>();
        for (var d = gridStart; d <= gridEnd; d = d.AddDays(1))
        {
            var inMonth = d >= monthStart && d < monthEnd;
            var occ = inMonth && occupancy.TryGetValue(d, out var o) ? o : DayOccupancy.Free;
            days.Add(new CalendarDay(d, inMonth, occ));
        }

        return days;
    }

    private static int OccupancyPercent(DateOnly monthStart, Dictionary<DateOnly, DayOccupancy> occupancy)
    {
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        var occupied = occupancy.Count(kvp => kvp.Value != DayOccupancy.Free);
        return daysInMonth == 0 ? 0 : (int)Math.Round(100.0 * occupied / daysInMonth, MidpointRounding.AwayFromZero);
    }

    private async Task<(int Count, decimal Revenue)> MonthBookingStatsAsync(DateOnly currentMonth, CancellationToken cancellationToken)
    {
        var startUtc = new DateTime(currentMonth.Year, currentMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endUtc = startUtc.AddMonths(1);

        var confirmedThisMonth = dbContext.Bookings.AsNoTracking()
            .Where(b => b.ConfirmedAtUtc != null && b.ConfirmedAtUtc >= startUtc && b.ConfirmedAtUtc < endUtc);

        var count = await confirmedThisMonth.CountAsync(cancellationToken);
        var totals = await confirmedThisMonth
            .Where(b => ActiveStayStatuses.Contains(b.Status))
            .Select(b => b.Total)
            .ToListAsync(cancellationToken);

        return (count, totals.Sum(m => m.Amount));
    }

    private async Task<NextCheckIn?> NextCheckInAsync(DateOnly today, CancellationToken cancellationToken)
    {
        var next = await dbContext.Bookings.AsNoTracking()
            .Where(b => (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn) && b.CheckIn >= today)
            .OrderBy(b => b.CheckIn)
            .Select(b => new NextCheckIn(b.ReferenceCode, b.GuestName, b.CheckIn))
            .FirstOrDefaultAsync(cancellationToken);
        return next;
    }

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

    private async Task<(bool Manual, string Status)> PricingStatusAsync(CancellationToken cancellationToken)
    {
        if (pricingOptions.Value.Provider == RateProviderType.None)
        {
            return (true, "Manual pricing — set nightly rates yourself.");
        }

        var lastSync = await dbContext.DailyRates.AsNoTracking()
            .Where(d => d.Source != RateSource.Manual && d.SourceUpdatedAtUtc != null)
            .MaxAsync(d => (DateTime?)d.SourceUpdatedAtUtc, cancellationToken);
        var last = lastSync?.ToString("u", CultureInfo.InvariantCulture) ?? "never";
        return (false, $"{pricingOptions.Value.Provider} · last sync {last}");
    }

    private async Task<IReadOnlyList<ConflictItem>> ConflictsAsync(CancellationToken cancellationToken) =>
        // Resolved conflicts (Stage 7 §7 calendar) stay queryable there; the dashboard shows open ones.
        await dbContext.BookingConflicts.AsNoTracking()
            .Where(c => c.ResolvedAtUtc == null)
            .OrderByDescending(c => c.DetectedAtUtc)
            .Take(50)
            .Select(c => new ConflictItem(c.BookingReference, c.SourceName, c.StartDate, c.EndDate, c.DetectedAtUtc))
            .ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<MultibancoPendingItem>> MultibancoPendingAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.AwaitingPayment && b.MultibancoReference != null)
            .OrderBy(b => b.PaymentExpiresAtUtc)
            .Select(b => new { b.ReferenceCode, b.Total, b.MultibancoEntity, b.MultibancoReference, b.PaymentExpiresAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .Select(b => new MultibancoPendingItem(b.ReferenceCode, b.Total.Amount, b.MultibancoEntity, b.MultibancoReference, b.PaymentExpiresAtUtc))
            .ToList();
    }
}
