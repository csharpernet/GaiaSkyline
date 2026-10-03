using GaiaSkyline.Application.Notifications;
using Hangfire;

namespace GaiaSkyline.BackgroundJobs;

/// <summary>
/// Enqueues booking emails as Hangfire jobs, so the webhook returns immediately and delivery is
/// retried automatically if it fails.
/// </summary>
internal sealed class HangfireEmailJobScheduler(IBackgroundJobClient backgroundJobs) : IEmailJobScheduler
{
    public Task EnqueueAsync(Guid bookingId, BookingEmailKind kind, CancellationToken cancellationToken)
    {
        backgroundJobs.Enqueue<IBookingEmailDispatcher>(
            dispatcher => dispatcher.DispatchAsync(bookingId, kind, CancellationToken.None));
        return Task.CompletedTask;
    }
}
