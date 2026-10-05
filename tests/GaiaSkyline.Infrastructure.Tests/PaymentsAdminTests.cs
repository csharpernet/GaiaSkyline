using FluentAssertions;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Payments;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Availability;
using GaiaSkyline.Infrastructure.Bookings;
using GaiaSkyline.Infrastructure.Payments;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stripe;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>The payments admin (Stage 7 §9): event re-processing, the event browser, refund history and the Multibanco monitor.</summary>
public sealed class PaymentsAdminTests : IClassFixture<LocalDbFixture>, IDisposable
{
    private readonly LocalDbFixture _fixture;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public PaymentsAdminTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        using var context = _fixture.CreateContext();
        context.StripeEventLogs.ExecuteDelete();
        context.BookingDateOccupancies.ExecuteDelete();
        context.Bookings.ExecuteDelete();
    }

    public void Dispose() => _cache.Dispose();

    private static readonly Lazy<IStripeClient> NeverUsedClient = new(
        () => throw new InvalidOperationException("The Stripe client must not be touched in this test."));

    private static PaymentsAdminReadService Read(AppDbContext context, string secretKey = "") => new(
        context, NeverUsedClient,
        Options.Create(new StripeOptions { SecretKey = secretKey }),
        NullLogger<PaymentsAdminReadService>.Instance);

    private StripeWebhookHandler Handler(AppDbContext context)
    {
        var availability = new AvailabilityService(context, _cache, new AvailabilityCacheState());
        var ics = new IcsCacheInvalidator();
        var lifecycle = new BookingLifecycleService(context, availability, ics, TimeProvider.System);
        return new StripeWebhookHandler(
            context, Options.Create(new StripeOptions { WebhookSecret = "whsec_reprocess" }),
            lifecycle, availability, ics, new NoNotifications(), TimeProvider.System);
    }

    [Fact]
    public async Task Reprocess_runs_a_failed_event_from_its_stored_payload_and_confirms_the_booking()
    {
        var bookingId = await SeedBookingAsync("pi_reprocess");
        Guid logId;
        await using (var seed = _fixture.CreateContext())
        {
            var log = new StripeEventLog(
                StripeEventLogId.New(), "evt_reprocess_1", "payment_intent.succeeded",
                PaymentIntentEvent("evt_reprocess_1", "payment_intent.succeeded", "pi_reprocess"), DateTime.UtcNow);
            log.MarkFailed("first attempt blew up");
            seed.StripeEventLogs.Add(log);
            await seed.SaveChangesAsync();
            logId = log.Id.Value;
        }

        await using var context = _fixture.CreateContext();
        var result = await Handler(context).ReprocessAsync(logId, CancellationToken.None);

        result.Outcome.Should().Be(WebhookOutcome.Ok);
        await using var verify = _fixture.CreateContext();
        (await verify.Bookings.FirstAsync(b => b.Id == bookingId)).Status.Should().Be(BookingStatus.Confirmed);
        var log2 = await verify.StripeEventLogs.FirstAsync(e => e.StripeEventId == "evt_reprocess_1");
        log2.ProcessedAtUtc.Should().NotBeNull();
        log2.Error.Should().BeNull();

        (await Handler(context).ReprocessAsync(Guid.NewGuid(), CancellationToken.None))
            .Outcome.Should().Be(WebhookOutcome.Error, "an unknown event id fails loudly");
    }

    [Fact]
    public async Task Refund_history_parses_stored_charge_refunded_payloads_and_joins_the_booking()
    {
        await SeedBookingAsync("pi_refund_hist");
        await using (var seed = _fixture.CreateContext())
        {
            var partial = new StripeEventLog(
                StripeEventLogId.New(), "evt_ref_1", "charge.refunded",
                ChargeRefundedEvent("evt_ref_1", "pi_refund_hist", 36000, 15000), DateTime.UtcNow);
            partial.MarkProcessed(DateTime.UtcNow);
            seed.StripeEventLogs.Add(partial);
            seed.StripeEventLogs.Add(new StripeEventLog(
                StripeEventLogId.New(), "evt_ref_broken", "charge.refunded", "{not json", DateTime.UtcNow));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var history = await Read(context).GetRefundHistoryAsync(CancellationToken.None);

        var row = history.Should().ContainSingle().Subject;
        row.AmountRefundedEur.Should().Be(150m);
        row.FullyRefunded.Should().BeFalse();
        row.BookingReference.Should().NotBeNull("the payment intent joins back to the booking");
    }

    [Fact]
    public async Task Event_browser_filters_by_type_and_failure_state()
    {
        await using (var seed = _fixture.CreateContext())
        {
            var ok = new StripeEventLog(StripeEventLogId.New(), "evt_ok", "payment_intent.succeeded", "{}", DateTime.UtcNow);
            ok.MarkProcessed(DateTime.UtcNow);
            seed.StripeEventLogs.Add(ok);
            var failed = new StripeEventLog(StripeEventLogId.New(), "evt_fail", "charge.refunded", "{}", DateTime.UtcNow);
            failed.MarkFailed("boom");
            seed.StripeEventLogs.Add(failed);
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var read = Read(context);

        var all = await read.GetEventsAsync(new StripeEventFilter(), CancellationToken.None);
        all.TotalCount.Should().Be(2);
        all.Types.Should().Contain(["payment_intent.succeeded", "charge.refunded"]);

        var failedOnly = await read.GetEventsAsync(new StripeEventFilter(OnlyFailed: true), CancellationToken.None);
        failedOnly.Items.Should().ContainSingle().Which.StripeEventId.Should().Be("evt_fail");

        var byType = await read.GetEventsAsync(new StripeEventFilter(Type: "payment_intent.succeeded"), CancellationToken.None);
        byType.Items.Should().ContainSingle().Which.StripeEventId.Should().Be("evt_ok");
    }

    [Fact]
    public async Task Multibanco_monitor_lists_only_awaiting_holds_with_a_voucher_and_status_degrades_unconfigured()
    {
        var withVoucher = await SeedBookingAsync("pi_mb_1", multibanco: true);
        await SeedBookingAsync("pi_card_1");

        await using var context = _fixture.CreateContext();
        var read = Read(context);

        var holds = await read.GetMultibancoPendingAsync(CancellationToken.None);
        holds.Should().ContainSingle().Which.BookingId.Should().Be(withVoucher.Value);

        var status = await read.GetStripeStatusAsync(CancellationToken.None);
        status.Configured.Should().BeFalse();
        status.AccountSummary.Should().Contain("not configured");

        (await read.GetOpenDisputesAsync(CancellationToken.None)).Should().BeEmpty("no key — never calls Stripe");
    }

    private async Task<BookingId> SeedBookingAsync(string paymentIntentId, bool multibanco = false)
    {
        await using var context = _fixture.CreateContext();
        var id = BookingId.New();
        var checkIn = new DateOnly(2031, 3, 1);
        var booking = new Booking(
            id, $"PA{Guid.NewGuid():N}"[..12].ToUpperInvariant(), checkIn, checkIn.AddDays(3), 2, 0, 0,
            "Guest", "pa@example.com", "+351 912 345 678", "PT", "en",
            new Money(100m, "EUR"), new Money(300m, "EUR"), new Money(0m, "EUR"),
            new Money(60m, "EUR"), new Money(0m, "EUR"), new Money(360m, "EUR"), DateTime.UtcNow);
        booking.AttachPaymentIntent(paymentIntentId);
        if (multibanco)
        {
            booking.SetMultibancoVoucher("12345", "999888777", DateTime.UtcNow.AddDays(10));
        }

        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        return id;
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

    private sealed class NoNotifications : GaiaSkyline.Application.Notifications.IBookingNotificationService
    {
        public Task SendGuestConfirmationAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendOwnerNotificationAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendMultibancoReferenceAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendPaymentExpiredAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendRefundAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendDisputeAlertAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendPropertyManagerNotificationAsync(Booking booking, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
