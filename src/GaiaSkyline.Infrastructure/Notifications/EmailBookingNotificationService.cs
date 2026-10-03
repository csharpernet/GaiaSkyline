using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Bookings;

namespace GaiaSkyline.Infrastructure.Notifications;

/// <summary>
/// Real booking notifications (replaces the Increment-C no-op): each lifecycle event schedules the
/// matching localized email via <see cref="IEmailJobScheduler"/> (Hangfire in dev/prod, inline when
/// background processing is off).
/// </summary>
internal sealed class EmailBookingNotificationService(IEmailJobScheduler scheduler) : IBookingNotificationService
{
    public Task SendGuestConfirmationAsync(Booking booking, CancellationToken cancellationToken) =>
        scheduler.EnqueueAsync(booking.Id.Value, BookingEmailKind.Confirmation, cancellationToken);

    public Task SendOwnerNotificationAsync(Booking booking, CancellationToken cancellationToken) =>
        scheduler.EnqueueAsync(booking.Id.Value, BookingEmailKind.OwnerNotification, cancellationToken);

    public Task SendMultibancoReferenceAsync(Booking booking, CancellationToken cancellationToken) =>
        scheduler.EnqueueAsync(booking.Id.Value, BookingEmailKind.MultibancoReference, cancellationToken);

    public Task SendPaymentExpiredAsync(Booking booking, CancellationToken cancellationToken) =>
        scheduler.EnqueueAsync(booking.Id.Value, BookingEmailKind.PaymentExpired, cancellationToken);

    public Task SendRefundAsync(Booking booking, CancellationToken cancellationToken) =>
        scheduler.EnqueueAsync(booking.Id.Value, BookingEmailKind.Refund, cancellationToken);

    public Task SendDisputeAlertAsync(Booking booking, CancellationToken cancellationToken) =>
        scheduler.EnqueueAsync(booking.Id.Value, BookingEmailKind.DisputeAlert, cancellationToken);
}
