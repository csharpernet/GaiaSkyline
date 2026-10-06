using GaiaSkyline.Domain.Bookings;

namespace GaiaSkyline.Application.Admin;

/// <summary>What the owner must do in the management system (Hostify) for a changed direct booking.</summary>
public enum ManualSyncAction
{
    BlockInHostify,
    UnblockInHostify,
}

/// <summary>A direct booking whose latest change (confirm/cancel) has not yet been mirrored in Hostify.</summary>
public sealed record ManualSyncItem(
    string Reference,
    string GuestName,
    DateOnly CheckIn,
    DateOnly CheckOut,
    ManualSyncAction Action,
    DateTime ChangeAtUtc,
    bool Overdue);

/// <summary>What occupies a calendar day (priority order when several overlap).</summary>
public enum DayOccupancy
{
    Free,
    DirectBooking,
    ImportedBlock,
    ExternalOwnerBlock,
    OwnerUnavailable,
}

public sealed record CalendarDay(DateOnly Date, bool InMonth, DayOccupancy Occupancy);

public sealed record ConflictItem(string BookingReference, string SourceName, DateOnly StartDate, DateOnly EndDate, DateTime DetectedAtUtc);

public sealed record MultibancoPendingItem(string Reference, decimal Total, string? Entity, string? MultibancoReference, DateTime? ExpiresAtUtc);

public sealed record NextCheckIn(string Reference, string GuestName, DateOnly CheckIn);

/// <summary>Everything the admin dashboard renders for the given (navigable) calendar month.
/// <paramref name="OpenRateSyncRejections"/> counts the provider rate rejections awaiting the owner's
/// review on /admin/prices (Stage 7 §1).</summary>
public sealed record DashboardSummary(
    int BookingsThisMonth,
    int OccupancyPercent,
    decimal RevenueMonthToDate,
    NextCheckIn? NextCheckIn,
    DateOnly Month,
    IReadOnlyList<CalendarDay> CalendarDays,
    bool CalendarManualMode,
    string CalendarStatus,
    bool PricingManualMode,
    string PricingStatus,
    IReadOnlyList<ManualSyncItem> ManualSyncItems,
    IReadOnlyList<ConflictItem> Conflicts,
    IReadOnlyList<MultibancoPendingItem> MultibancoPending,
    int OpenRateSyncRejections = 0);

/// <summary>Reads the owner dashboard and drives the manual Hostify-sync to-do (manual mode only).</summary>
public interface IDashboardService
{
    Task<DashboardSummary> GetAsync(DateOnly month, CancellationToken cancellationToken);

    /// <summary>The outstanding manual-sync items (reused by the daily reminder job).</summary>
    Task<IReadOnlyList<ManualSyncItem>> GetOutstandingManualSyncAsync(CancellationToken cancellationToken);

    /// <summary>Marks a booking's current state as mirrored in Hostify (clears it from the to-do).</summary>
    Task MarkSyncedAsync(string reference, string? note, Guid? actorUserId, string? actorIp, CancellationToken cancellationToken);
}

/// <summary>Sends the owner a daily reminder of outstanding manual-sync items (only when overdue).</summary>
public interface IManualSyncReminderService
{
    Task SendDueRemindersAsync(CancellationToken cancellationToken);
}

/// <summary>Bookings whose confirm/cancel must be mirrored in Hostify count toward the manual to-do.</summary>
public static class ManualSyncStatuses
{
    public static bool NeedsMirroring(BookingStatus status) =>
        status is BookingStatus.Confirmed or BookingStatus.Cancelled;
}
