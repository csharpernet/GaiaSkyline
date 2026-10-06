using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Availability;
using GaiaSkyline.Infrastructure.Bookings;
using GaiaSkyline.Infrastructure.Payments;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class StripeLifecycleTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>, IDisposable
{
    private const string WebhookSecret = "whsec_test_secret_abc123";

    private readonly LocalDbFixture _fixture = fixture;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly AvailabilityCacheState _cacheState = new();

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task An_invalid_signature_is_rejected()
    {
        await using var context = _fixture.CreateContext();
        var (handler, _) = BuildHandler(context);
        var payload = PaymentIntentEvent("evt_badsig", "payment_intent.succeeded", "pi_x");

        var result = await handler.HandleAsync(payload, "t=1,v1=deadbeef", CancellationToken.None);

        result.Outcome.Should().Be(WebhookOutcome.InvalidSignature);
    }

    [Fact]
    public async Task Payment_succeeded_confirms_the_booking_and_is_idempotent()
    {
        var id = await SeedBookingAsync("pi_succeed", new DateOnly(2027, 9, 1));
        var payload = PaymentIntentEvent("evt_succeed_1", "payment_intent.succeeded", "pi_succeed");

        RecordingNotifications notifications;
        await using (var context = _fixture.CreateContext())
        {
            var (handler, notifs) = BuildHandler(context);
            notifications = notifs;
            var first = await handler.HandleAsync(payload, Sign(payload), CancellationToken.None);
            first.Outcome.Should().Be(WebhookOutcome.Ok);
        }

        // Same event again → no-op (idempotent).
        await using (var context = _fixture.CreateContext())
        {
            var (handler, _) = BuildHandler(context);
            var second = await handler.HandleAsync(payload, Sign(payload), CancellationToken.None);
            second.Outcome.Should().Be(WebhookOutcome.Ok);
        }

        await using (var verify = _fixture.CreateContext())
        {
            (await verify.Bookings.FirstAsync(b => b.Id == id)).Status.Should().Be(BookingStatus.Confirmed);
            (await verify.StripeEventLogs.CountAsync(e => e.StripeEventId == "evt_succeed_1")).Should().Be(1);
        }

        notifications.GuestConfirmations.Should().Be(1);
        notifications.OwnerNotifications.Should().Be(1);
        notifications.PropertyManagerNotifications.Should().Be(1);
    }

    [Fact]
    public async Task Multibanco_payment_failed_cancels_and_releases_the_dates()
    {
        var checkIn = new DateOnly(2027, 10, 1);
        var id = await SeedBookingAsync("pi_mb_fail", checkIn, multibanco: true);
        var payload = PaymentIntentEvent("evt_mb_fail", "payment_intent.payment_failed", "pi_mb_fail");

        await using (var context = _fixture.CreateContext())
        {
            var (handler, _) = BuildHandler(context);
            var result = await handler.HandleAsync(payload, Sign(payload), CancellationToken.None);
            result.Outcome.Should().Be(WebhookOutcome.Ok);
        }

        await using (var verify = _fixture.CreateContext())
        {
            (await verify.Bookings.FirstAsync(b => b.Id == id)).Status.Should().Be(BookingStatus.Cancelled);
            (await verify.BookingDateOccupancies.CountAsync(o => o.BookingId == id)).Should().Be(0);
        }
    }

    [Fact]
    public async Task Card_payment_failed_keeps_the_booking_awaiting_payment()
    {
        var id = await SeedBookingAsync("pi_card_fail", new DateOnly(2027, 11, 1));
        var payload = PaymentIntentEvent("evt_card_fail", "payment_intent.payment_failed", "pi_card_fail");

        await using (var context = _fixture.CreateContext())
        {
            var (handler, _) = BuildHandler(context);
            await handler.HandleAsync(payload, Sign(payload), CancellationToken.None);
        }

        await using (var verify = _fixture.CreateContext())
        {
            (await verify.Bookings.FirstAsync(b => b.Id == id)).Status.Should().Be(BookingStatus.AwaitingPayment);
            (await verify.BookingDateOccupancies.CountAsync(o => o.BookingId == id)).Should().Be(3);
        }
    }

    [Fact]
    public async Task Charge_refunded_marks_a_confirmed_booking_refunded()
    {
        var id = await SeedBookingAsync("pi_refund", new DateOnly(2027, 12, 1), confirmed: true);
        var payload = ChargeRefundedEvent("evt_refund", "pi_refund", amount: 36000, amountRefunded: 36000);

        RecordingNotifications notifications;
        await using (var context = _fixture.CreateContext())
        {
            var (handler, notifs) = BuildHandler(context);
            notifications = notifs;
            await handler.HandleAsync(payload, Sign(payload), CancellationToken.None);
        }

        await using (var verify = _fixture.CreateContext())
        {
            (await verify.Bookings.FirstAsync(b => b.Id == id)).Status.Should().Be(BookingStatus.Refunded);
        }

        notifications.Refunds.Should().Be(1);
    }

    [Fact]
    public async Task Expiry_releases_a_stale_card_hold_but_not_a_fresh_one()
    {
        var stale = await SeedBookingAsync(
            "pi_stale", new DateOnly(2028, 1, 1), createdAtUtc: DateTime.UtcNow.AddMinutes(-45));
        var fresh = await SeedBookingAsync(
            "pi_fresh", new DateOnly(2028, 2, 1), createdAtUtc: DateTime.UtcNow.AddMinutes(-5));

        await using (var context = _fixture.CreateContext())
        {
            var released = await BuildExpiryService(context).ExpireUnpaidHoldsAsync(CancellationToken.None);
            released.Should().Be(1);
        }

        await using (var verify = _fixture.CreateContext())
        {
            (await verify.Bookings.FirstAsync(b => b.Id == stale)).Status.Should().Be(BookingStatus.Cancelled);
            (await verify.Bookings.FirstAsync(b => b.Id == fresh)).Status.Should().Be(BookingStatus.AwaitingPayment);
            (await verify.BookingDateOccupancies.CountAsync(o => o.BookingId == stale)).Should().Be(0);
            (await verify.BookingDateOccupancies.CountAsync(o => o.BookingId == fresh)).Should().Be(3);
        }
    }

    private (StripeWebhookHandler Handler, RecordingNotifications Notifications) BuildHandler(AppDbContext context)
    {
        var availability = new AvailabilityService(context, _cache, _cacheState);
        var ics = new IcsCacheInvalidator();
        var lifecycle = new BookingLifecycleService(context, availability, ics, TimeProvider.System);
        var notifications = new RecordingNotifications();
        var options = Options.Create(new StripeOptions { WebhookSecret = WebhookSecret, UnpaidHoldMinutes = 30, MultibancoMinLeadDays = 10 });
        var handler = new StripeWebhookHandler(
            context, options, lifecycle, availability, ics, notifications,
            new GaiaSkyline.Infrastructure.Partners.PartnerAttributionService(
                context, TimeProvider.System,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<GaiaSkyline.Infrastructure.Partners.PartnerAttributionService>.Instance),
            TimeProvider.System);
        return (handler, notifications);
    }

    private BookingExpiryService BuildExpiryService(AppDbContext context)
    {
        var availability = new AvailabilityService(context, _cache, _cacheState);
        var lifecycle = new BookingLifecycleService(context, availability, new IcsCacheInvalidator(), TimeProvider.System);
        var options = Options.Create(new StripeOptions { UnpaidHoldMinutes = 30, MultibancoMinLeadDays = 10 });
        return new BookingExpiryService(context, lifecycle, options, TimeProvider.System);
    }

    private async Task<BookingId> SeedBookingAsync(
        string paymentIntentId, DateOnly checkIn, bool multibanco = false, bool confirmed = false, DateTime? createdAtUtc = null)
    {
        await using var context = _fixture.CreateContext();
        var id = BookingId.New();
        var checkOut = checkIn.AddDays(3);
        var booking = new Booking(
            id, new BookingReferenceGenerator().Next(), checkIn, checkOut, 2, 0, 0,
            "Test Guest", "guest@example.com", "+351 912 345 678", "Portugal", "pt-PT",
            nightlyRateSnapshot: Eur(100), subtotal: Eur(300), discountAmount: Eur(0),
            cleaningFee: Eur(60), touristTax: Eur(0), total: Eur(360),
            createdAtUtc: createdAtUtc ?? DateTime.UtcNow);
        booking.AttachStripeCustomer("cus_test");
        booking.AttachPaymentIntent(paymentIntentId);
        if (multibanco)
        {
            booking.SetMultibancoVoucher("12345", "999888777", DateTime.UtcNow.AddDays(5));
        }

        if (confirmed)
        {
            booking.ConfirmPayment("card", DateTime.UtcNow);
        }

        context.Bookings.Add(booking);
        for (var night = checkIn; night < checkOut; night = night.AddDays(1))
        {
            context.BookingDateOccupancies.Add(new BookingDateOccupancy(night, id));
        }

        await context.SaveChangesAsync();
        return id;
    }

    private static Money Eur(decimal amount) => new(amount, "EUR");

    private static string Sign(string payload)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(WebhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string PaymentIntentEvent(string eventId, string type, string paymentIntentId) =>
        """
        {"id":"__EVT__","object":"event","type":"__TYPE__","data":{"object":{"id":"__PI__","object":"payment_intent","amount":36000,"currency":"eur","status":"succeeded","payment_method_types":["card"]}}}
        """
        .Replace("__EVT__", eventId, StringComparison.Ordinal)
        .Replace("__TYPE__", type, StringComparison.Ordinal)
        .Replace("__PI__", paymentIntentId, StringComparison.Ordinal);

    private static string ChargeRefundedEvent(string eventId, string paymentIntentId, long amount, long amountRefunded) =>
        """
        {"id":"__EVT__","object":"event","type":"charge.refunded","data":{"object":{"id":"ch_test","object":"charge","payment_intent":"__PI__","amount":__AMT__,"amount_refunded":__REFUNDED__}}}
        """
        .Replace("__EVT__", eventId, StringComparison.Ordinal)
        .Replace("__PI__", paymentIntentId, StringComparison.Ordinal)
        .Replace("__AMT__", amount.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("__REFUNDED__", amountRefunded.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

    private sealed class RecordingNotifications : IBookingNotificationService
    {
        public int GuestConfirmations { get; private set; }

        public int OwnerNotifications { get; private set; }

        public int MultibancoReferences { get; private set; }

        public int PaymentExpiries { get; private set; }

        public int Refunds { get; private set; }

        public int DisputeAlerts { get; private set; }

        public int PropertyManagerNotifications { get; private set; }

        public Task SendGuestConfirmationAsync(Booking booking, CancellationToken cancellationToken)
        {
            GuestConfirmations++;
            return Task.CompletedTask;
        }

        public Task SendOwnerNotificationAsync(Booking booking, CancellationToken cancellationToken)
        {
            OwnerNotifications++;
            return Task.CompletedTask;
        }

        public Task SendMultibancoReferenceAsync(Booking booking, CancellationToken cancellationToken)
        {
            MultibancoReferences++;
            return Task.CompletedTask;
        }

        public Task SendPaymentExpiredAsync(Booking booking, CancellationToken cancellationToken)
        {
            PaymentExpiries++;
            return Task.CompletedTask;
        }

        public Task SendRefundAsync(Booking booking, CancellationToken cancellationToken)
        {
            Refunds++;
            return Task.CompletedTask;
        }

        public Task SendDisputeAlertAsync(Booking booking, CancellationToken cancellationToken)
        {
            DisputeAlerts++;
            return Task.CompletedTask;
        }

        public Task SendPropertyManagerNotificationAsync(Booking booking, CancellationToken cancellationToken)
        {
            PropertyManagerNotifications++;
            return Task.CompletedTask;
        }
    }
}
