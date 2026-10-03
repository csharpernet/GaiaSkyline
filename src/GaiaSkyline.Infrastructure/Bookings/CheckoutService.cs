using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Infrastructure.Persistence;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>
/// Creates the booking (holding its dates), then the Stripe PaymentIntent, attaches the Stripe
/// customer + intent to the booking, and returns the client secret with a signed confirmation token.
/// </summary>
internal sealed class CheckoutService(
    IBookingCreationService bookingCreation,
    IPaymentService paymentService,
    IBookingTokenService tokenService,
    AppDbContext dbContext) : ICheckoutService
{
    public async Task<CheckoutResult> StartAsync(CreateBookingCommand command, CancellationToken cancellationToken)
    {
        var booking = await bookingCreation.CreateAsync(command, cancellationToken);
        var intent = await paymentService.CreatePaymentIntentAsync(booking, cancellationToken);

        // booking is tracked by the same scoped context used for creation.
        booking.AttachStripeCustomer(intent.CustomerId);
        booking.AttachPaymentIntent(intent.PaymentIntentId);
        await dbContext.SaveChangesAsync(cancellationToken);

        var token = tokenService.CreateConfirmationToken(booking.ReferenceCode);
        return new CheckoutResult(booking.ReferenceCode, intent.ClientSecret, token);
    }
}
