using System.Globalization;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Bookings;
using Microsoft.Extensions.Options;
using Stripe;

namespace GaiaSkyline.Infrastructure.Payments;

/// <summary>
/// Creates Stripe customers and payment intents with automatic capture (ADR 0010). Multibanco is
/// offered via automatic payment methods when there is enough lead time, and excluded with an
/// explicit card-only method list when check-in is too close.
/// </summary>
internal sealed class StripePaymentService(
    IStripeClient client,
    IOptions<StripeOptions> options,
    TimeProvider clock) : IPaymentService
{
    private readonly StripeOptions _options = options.Value;

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(Booking booking, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var customerId = await GetOrCreateCustomerAsync(booking, cancellationToken);
        var amountCents = (long)Math.Round(booking.Total.Amount * 100m, MidpointRounding.AwayFromZero);
        var allowMultibanco = MultibancoPolicy.IsAllowed(
            booking.CheckIn, LisbonClock.Today(clock), _options.MultibancoMinLeadDays);

        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = "eur",
            Customer = customerId,
            ReceiptEmail = booking.GuestEmail,
            Description =
                $"Gaia Skyline Apartment — {booking.Nights} nights, " +
                $"{booking.CheckIn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}–" +
                $"{booking.CheckOut.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
            StatementDescriptorSuffix = "GAIA SKYLINE",
            Metadata = new Dictionary<string, string>
            {
                ["booking_id"] = booking.Id.ToString(),
                ["reference"] = booking.ReferenceCode,
            },
        };

        // Dashboard-enabled methods (cards, wallets, Link, Multibanco…) appear automatically.
        createOptions.AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true };
        if (!allowMultibanco)
        {
            // Too close to check-in for Multibanco's lead time: drop just that method.
            createOptions.ExcludedPaymentMethodTypes = ["multibanco"];
        }

        var intentService = new PaymentIntentService(client);
        var intent = await intentService.CreateAsync(
            createOptions,
            new RequestOptions { IdempotencyKey = $"booking-{booking.Id}-pi" },
            cancellationToken);

        return new PaymentIntentResult(intent.Id, intent.ClientSecret, customerId);
    }

    private async Task<string> GetOrCreateCustomerAsync(Booking booking, CancellationToken cancellationToken)
    {
        var customerService = new CustomerService(client);
        var existing = await customerService.ListAsync(
            new CustomerListOptions { Email = booking.GuestEmail, Limit = 1 },
            cancellationToken: cancellationToken);

        if (existing.Data.Count > 0)
        {
            return existing.Data[0].Id;
        }

        var created = await customerService.CreateAsync(
            new CustomerCreateOptions { Email = booking.GuestEmail, Name = booking.GuestName },
            cancellationToken: cancellationToken);
        return created.Id;
    }
}
