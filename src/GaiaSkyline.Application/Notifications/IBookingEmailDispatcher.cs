using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Application.Notifications;

/// <summary>
/// Renders and sends one booking email. Public so it can be invoked as a Hangfire job (the
/// <see cref="IBookingNotificationService"/> enqueues calls to it).
/// </summary>
public interface IBookingEmailDispatcher
{
    Task DispatchAsync(Guid bookingId, BookingEmailKind kind, CancellationToken cancellationToken);
}
