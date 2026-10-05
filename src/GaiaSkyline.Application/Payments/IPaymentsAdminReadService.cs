namespace GaiaSkyline.Application.Payments;

/// <summary>Live Stripe account state for the admin panel; everything degrades gracefully when unconfigured.</summary>
public sealed record StripeStatusDto(
    bool Configured,
    bool LiveMode,
    string AccountSummary,
    IReadOnlyList<string> PaymentMethods,
    DateTime? LastWebhookUtc);

public sealed record StripeEventFilter(string? Type = null, bool OnlyFailed = false, int Page = 1, int PageSize = 50);

public sealed record StripeEventRowDto(
    Guid Id,
    string StripeEventId,
    string Type,
    DateTime ReceivedAtUtc,
    DateTime? ProcessedAtUtc,
    string? Error);

public sealed record StripeEventPageDto(
    IReadOnlyList<StripeEventRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<string> Types);

/// <summary>A processed <c>charge.refunded</c> event, joined to the booking where the intent matches.</summary>
public sealed record RefundRowDto(
    DateTime ReceivedAtUtc,
    string PaymentIntentId,
    string? BookingReference,
    Guid? BookingId,
    decimal AmountRefundedEur,
    bool FullyRefunded);

public sealed record DisputeRowDto(
    string DisputeId,
    string? PaymentIntentId,
    string? BookingReference,
    Guid? BookingId,
    decimal AmountEur,
    string Reason,
    string Status,
    DateTime? EvidenceDueUtc,
    string DashboardUrl);

public sealed record MultibancoRowDto(
    Guid BookingId,
    string Reference,
    decimal TotalEur,
    string? Entity,
    string? MultibancoReference,
    DateTime? ExpiresAtUtc);

/// <summary>
/// Owner-only payments reads (Stage 7 §9): live Stripe status, the StripeEventLog browser, refund
/// history derived from <c>charge.refunded</c> payloads, open disputes from the Stripe API, and the
/// Multibanco holds monitor.
/// </summary>
public interface IPaymentsAdminReadService
{
    Task<StripeStatusDto> GetStripeStatusAsync(CancellationToken cancellationToken);

    Task<StripeEventPageDto> GetEventsAsync(StripeEventFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefundRowDto>> GetRefundHistoryAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DisputeRowDto>> GetOpenDisputesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<MultibancoRowDto>> GetMultibancoPendingAsync(CancellationToken cancellationToken);
}
