namespace GaiaSkyline.Application.Bookings;

/// <summary>Outcome of an admin booking action. <see cref="Error"/> is a user-facing message when it fails.</summary>
public sealed record BookingActionResult(bool Ok, string? Error)
{
    public static BookingActionResult Success { get; } = new(true, null);

    public static BookingActionResult Fail(string error) => new(false, error);
}

/// <summary>
/// Owner-only booking state changes for the admin manager (Stage 7 §6). Each honors the domain status
/// machine and returns a <see cref="BookingActionResult"/> rather than throwing on an illegal move.
/// Cancellation delegates to the lifecycle service so the held nights are released in one transaction.
/// </summary>
public interface IAdminBookingService
{
    Task<BookingActionResult> CheckInAsync(Guid id, CancellationToken cancellationToken);

    Task<BookingActionResult> CompleteAsync(Guid id, CancellationToken cancellationToken);

    Task<BookingActionResult> CancelAsync(Guid id, string? reason, CancellationToken cancellationToken);

    /// <summary>Marks the booking refunded or partially refunded (no Stripe refund — the owner reconciles that).</summary>
    Task<BookingActionResult> RefundAsync(Guid id, bool partial, CancellationToken cancellationToken);

    Task<BookingActionResult> SetNotesAsync(Guid id, string? notes, CancellationToken cancellationToken);

    /// <summary>Records that the owner mirrored this booking's current state in the management system (Hostify).</summary>
    Task<BookingActionResult> MarkSyncedAsync(Guid id, string? note, CancellationToken cancellationToken);
}
