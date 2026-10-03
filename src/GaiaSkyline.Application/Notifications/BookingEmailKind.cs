namespace GaiaSkyline.Application.Notifications;

/// <summary>The transactional emails sent around a booking (content-block keys: <c>email.{kind}.*</c>).</summary>
public enum BookingEmailKind
{
    Confirmation,
    MultibancoReference,
    PaymentExpired,
    Refund,
    Cancellation,
    OwnerNotification,
    DisputeAlert,
}
