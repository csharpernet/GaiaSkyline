namespace GaiaSkyline.Application.Bookings;

/// <summary>What the checkout page needs to drive the Stripe Payment Element.</summary>
public sealed record CheckoutResult(string BookingReference, string ClientSecret, string ConfirmationToken);

/// <summary>
/// Orchestrates checkout: re-quotes, creates the booking (holding its dates), creates the Stripe
/// PaymentIntent, attaches it to the booking, and returns the client secret + a signed confirmation
/// token. The server never trusts a client-supplied price.
/// </summary>
public interface ICheckoutService
{
    Task<CheckoutResult> StartAsync(CreateBookingCommand command, CancellationToken cancellationToken);
}
