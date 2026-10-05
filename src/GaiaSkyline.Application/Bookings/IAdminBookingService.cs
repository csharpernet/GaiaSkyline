namespace GaiaSkyline.Application.Bookings;

/// <summary>Outcome of an admin booking action. <see cref="Error"/> is a user-facing message when it fails.</summary>
public sealed record BookingActionResult(bool Ok, string? Error)
{
    public static BookingActionResult Success { get; } = new(true, null);

    public static BookingActionResult Fail(string error) => new(false, error);
}

/// <summary>Outcome of a manual booking creation; <see cref="BookingId"/> is set on success.</summary>
public sealed record ManualBookingResult(bool Ok, string? Error, Guid? BookingId)
{
    public static ManualBookingResult Fail(string error) => new(false, error, null);
}

/// <summary>
/// A phone/walk-in booking entered by the owner (Stage 7 §6): no Stripe — the payment method and the
/// amount actually agreed are recorded directly, and the booking is confirmed immediately.
/// <see cref="AmountReceivedEur"/> overrides the quoted total (any difference folds into the discount
/// line so the stored lines still add up); null keeps the quoted price.
/// </summary>
public sealed record ManualBookingCommand(
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Adults,
    int Children,
    int Infants,
    string GuestName,
    string GuestEmail,
    string GuestPhone,
    string GuestCountry,
    string GuestLanguage,
    string PaymentMethod,
    decimal? AmountReceivedEur,
    string? SpecialRequests,
    string? Notes,
    bool SendGuestConfirmation);

/// <summary>The payment methods a manual booking may record (lowercase, mirrors Stripe's "card"/"multibanco" style).</summary>
public static class ManualPaymentMethods
{
    public static readonly IReadOnlyList<string> All = ["cash", "bank-transfer", "card-terminal", "other"];

    public static bool IsValid(string? method) => method is not null && All.Contains(method);
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

    /// <summary>
    /// Cancels the booking and releases its nights. When <paramref name="refundAmountEur"/> is positive the
    /// Stripe refund is issued first (the <c>charge.refunded</c> webhook then drives Refunded/PartiallyRefunded);
    /// a positive amount on a booking without a Stripe payment fails. The guest and the property manager are
    /// notified.
    /// </summary>
    Task<BookingActionResult> CancelAsync(Guid id, string? reason, decimal refundAmountEur, CancellationToken cancellationToken);

    /// <summary>
    /// Refunds the booking. With a Stripe payment the refund is issued through Stripe (full when
    /// <paramref name="amountEur"/> is null) and the status is also marked locally; without one it is a
    /// bookkeeping mark only (the owner reconciled the money elsewhere).
    /// </summary>
    Task<BookingActionResult> RefundAsync(Guid id, bool partial, decimal? amountEur, CancellationToken cancellationToken);

    Task<BookingActionResult> SetNotesAsync(Guid id, string? notes, CancellationToken cancellationToken);

    /// <summary>Records that the owner mirrored this booking's current state in the management system (Hostify).</summary>
    Task<BookingActionResult> MarkSyncedAsync(Guid id, string? note, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a manual (phone/walk-in) booking: held nights written atomically, confirmed immediately with
    /// the given payment method, owner + property-manager alerts sent (and the guest confirmation unless opted out).
    /// </summary>
    Task<ManualBookingResult> CreateManualAsync(ManualBookingCommand command, CancellationToken cancellationToken);
}
