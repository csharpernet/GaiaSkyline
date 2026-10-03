using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Application.Payments;

/// <summary>Creates Stripe payment intents for bookings (automatic capture; see ADR 0010).</summary>
public interface IPaymentService
{
    /// <summary>
    /// Creates (or reuses) a Stripe Customer keyed by the guest's email and a PaymentIntent for the
    /// booking total. Multibanco is offered only when check-in has enough lead time.
    /// </summary>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(Booking booking, CancellationToken cancellationToken);
}

/// <summary>Issues refunds against a booking's payment (full or partial).</summary>
public interface IRefundService
{
    /// <summary>
    /// Refunds <paramref name="amountEur"/> (or the full amount when null) for the booking. The
    /// resulting <c>charge.refunded</c> webhook drives the booking to Refunded/PartiallyRefunded.
    /// </summary>
    Task RefundAsync(BookingId bookingId, decimal? amountEur, string reason, CancellationToken cancellationToken);
}
