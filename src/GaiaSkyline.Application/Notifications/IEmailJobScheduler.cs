namespace GaiaSkyline.Application.Notifications;

/// <summary>
/// Schedules a booking email for delivery. The Hangfire implementation enqueues it (so the webhook
/// returns immediately and delivery is retried on failure); the inline fallback sends it directly
/// when background processing is switched off.
/// </summary>
public interface IEmailJobScheduler
{
    Task EnqueueAsync(Guid bookingId, BookingEmailKind kind, CancellationToken cancellationToken);
}
