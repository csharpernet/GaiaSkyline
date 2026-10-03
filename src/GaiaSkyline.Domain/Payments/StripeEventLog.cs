using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Payments;

/// <summary>
/// A received Stripe webhook event. The unique <see cref="StripeEventId"/> makes webhook processing
/// idempotent: a duplicate delivery is recognised and ignored. On a processing failure the
/// <see cref="Error"/> is recorded and the endpoint returns 500 so Stripe retries.
/// </summary>
public sealed class StripeEventLog : Entity<StripeEventLogId>
{
    // Required by EF Core's materialization.
    private StripeEventLog()
    {
    }

    public StripeEventLog(
        StripeEventLogId id,
        string stripeEventId,
        string type,
        string payloadJson,
        DateTime receivedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stripeEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(payloadJson);

        Id = id;
        StripeEventId = stripeEventId.Trim();
        Type = type.Trim();
        PayloadJson = payloadJson;
        ReceivedAtUtc = receivedAtUtc;
    }

    /// <summary>The Stripe event id (e.g. "evt_..."); unique.</summary>
    public string StripeEventId { get; private set; } = null!;

    public string Type { get; private set; } = null!;

    public string PayloadJson { get; private set; } = null!;

    public DateTime ReceivedAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string? Error { get; private set; }

    /// <summary>Marks the event handled successfully.</summary>
    public void MarkProcessed(DateTime processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        Error = null;
    }

    /// <summary>Records a processing failure so the handler can return 500 for a Stripe retry.</summary>
    public void MarkFailed(string error)
    {
        Error = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error.Trim();
    }
}
