using GaiaSkyline.Application.Notifications;

namespace GaiaSkyline.Infrastructure.Notifications;

/// <summary>Fallback scheduler used when background processing is off: sends the email inline.</summary>
internal sealed class InlineEmailJobScheduler(IBookingEmailDispatcher dispatcher) : IEmailJobScheduler
{
    public Task EnqueueAsync(Guid bookingId, BookingEmailKind kind, CancellationToken cancellationToken) =>
        dispatcher.DispatchAsync(bookingId, kind, cancellationToken);
}
