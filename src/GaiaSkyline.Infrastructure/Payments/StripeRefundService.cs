using System.Globalization;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace GaiaSkyline.Infrastructure.Payments;

/// <summary>
/// Issues Stripe refunds. The resulting <c>charge.refunded</c> webhook drives the booking to
/// Refunded / PartiallyRefunded and sends the refund email, so this method only creates the refund.
/// </summary>
internal sealed class StripeRefundService(IStripeClient client, AppDbContext dbContext) : IRefundService
{
    public async Task RefundAsync(BookingId bookingId, decimal? amountEur, string reason, CancellationToken cancellationToken)
    {
        var booking = await dbContext.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken)
            ?? throw new InvalidOperationException($"Booking {bookingId} was not found.");

        if (string.IsNullOrEmpty(booking.StripePaymentIntentId))
        {
            throw new InvalidOperationException($"Booking {bookingId} has no payment to refund.");
        }

        var createOptions = new RefundCreateOptions
        {
            PaymentIntent = booking.StripePaymentIntentId,
            Reason = "requested_by_customer",
            Metadata = new Dictionary<string, string> { ["reason"] = reason },
        };

        // A deterministic key makes a given refund (full, or a specific partial amount) idempotent.
        var amountKey = "full";
        if (amountEur is { } amount)
        {
            var cents = (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
            createOptions.Amount = cents;
            amountKey = cents.ToString(CultureInfo.InvariantCulture);
        }

        var refundService = new RefundService(client);
        await refundService.CreateAsync(
            createOptions,
            new RequestOptions { IdempotencyKey = $"booking-{bookingId}-refund-{amountKey}" },
            cancellationToken);
    }
}
