using GaiaSkyline.Domain.Bookings;

namespace GaiaSkyline.Application.Notifications;

/// <summary>
/// Sends the transactional emails around a booking's lifecycle. Increment C triggers these from the
/// webhook; the Razor templates (5 languages) and SMTP/SendGrid delivery are built in Increment D,
/// so the Increment-C implementation is a logging stub.
/// </summary>
public interface IBookingNotificationService
{
    Task SendGuestConfirmationAsync(Booking booking, CancellationToken cancellationToken);

    Task SendOwnerNotificationAsync(Booking booking, CancellationToken cancellationToken);

    Task SendMultibancoReferenceAsync(Booking booking, CancellationToken cancellationToken);

    Task SendPaymentExpiredAsync(Booking booking, CancellationToken cancellationToken);

    Task SendRefundAsync(Booking booking, CancellationToken cancellationToken);

    Task SendDisputeAlertAsync(Booking booking, CancellationToken cancellationToken);
}
