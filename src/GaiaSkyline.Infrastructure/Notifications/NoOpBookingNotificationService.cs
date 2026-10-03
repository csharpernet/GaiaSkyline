using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Domain.Bookings;

namespace GaiaSkyline.Infrastructure.Notifications;

/// <summary>
/// Placeholder notification service for Increment C: the booking lifecycle calls these seams, but the
/// Razor email templates (5 languages) and SMTP/SendGrid delivery are built in Increment D, which
/// replaces this registration. Here they are no-ops.
/// </summary>
internal sealed class NoOpBookingNotificationService : IBookingNotificationService
{
    public Task SendGuestConfirmationAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendOwnerNotificationAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendMultibancoReferenceAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendPaymentExpiredAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendRefundAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SendDisputeAlertAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;
}
