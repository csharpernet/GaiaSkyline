using FluentAssertions;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>
/// The admin booking read + action services: filters, status transitions, notes, sync, manual
/// (phone/walk-in) creation and refund flows. Stage 7 §6.
/// </summary>
public sealed class AdminBookingServiceTests(LocalDbFixture fixture) : IClassFixture<LocalDbFixture>
{
    private readonly LocalDbFixture _fixture = fixture;
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task List_filters_by_status_search_payment_method_and_sync_state()
    {
        var email = $"filter-{Guid.NewGuid():N}@example.com";
        var confirmed = Booking("CONFIRMED", email);
        confirmed.ConfirmPayment("card", Now);
        confirmed.MarkExternalChannelSynced(Now, "seed");
        var awaiting = Booking("AWAITING", email);
        var manual = Booking("MANUALPAY", email);
        manual.ConfirmPayment("cash", Now);
        await SeedAsync(confirmed, awaiting, manual);

        await using var ctx = _fixture.CreateContext();
        var read = Read(ctx);

        // Search alone → all three of mine.
        var all = await read.GetAsync(new BookingAdminFilter(null, email, null, null), CancellationToken.None);
        all.Should().HaveCount(3);

        // Search + status → the two confirmed.
        var onlyConfirmed = await read.GetAsync(
            new BookingAdminFilter(BookingStatus.Confirmed, email, null, null), CancellationToken.None);
        onlyConfirmed.Should().HaveCount(2);

        // Payment-method filter → only the cash one.
        var cash = await read.GetAsync(
            new BookingAdminFilter(null, email, null, null, PaymentMethod: "cash"), CancellationToken.None);
        cash.Should().ContainSingle().Which.ReferenceCode.Should().Be("MANUALPAY");

        // Manual-sync filter → synced=yes finds only the acknowledged one; synced=no the other two.
        var synced = await read.GetAsync(
            new BookingAdminFilter(null, email, null, null, Synced: true), CancellationToken.None);
        synced.Should().ContainSingle().Which.ReferenceCode.Should().Be("CONFIRMED");
        var unsynced = await read.GetAsync(
            new BookingAdminFilter(null, email, null, null, Synced: false), CancellationToken.None);
        unsynced.Should().HaveCount(2);
    }

    [Fact]
    public async Task Detail_exposes_the_legal_next_statuses_and_the_stripe_dashboard_link()
    {
        var booking = Booking($"REF{Guid.NewGuid():N}"[..12], "d@example.com");
        booking.AttachPaymentIntent("pi_detail_123");
        booking.ConfirmPayment("card", Now);
        await SeedAsync(booking);

        await using var ctx = _fixture.CreateContext();
        var detail = await Read(ctx).GetDetailAsync(booking.Id.Value, CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.Status.Should().Be(BookingStatus.Confirmed);
        detail.AllowedTransitions.Should().Contain(BookingStatus.CheckedIn)
            .And.Contain(BookingStatus.Cancelled)
            .And.NotContain(BookingStatus.Completed);
        detail.StripeDashboardUrl.Should().Be("https://dashboard.stripe.com/test/payments/pi_detail_123",
            "an empty/test secret key must produce the test-mode dashboard URL");
    }

    [Fact]
    public async Task Detail_prefills_the_policy_refund_for_a_paid_confirmed_booking()
    {
        // The policy (14 days → 100%, 7 → 50%, later → 0) comes from the pricing read store; check-in is far out → 100%.
        var booking = Booking($"PF{Guid.NewGuid():N}"[..12], "pf@example.com", checkIn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60));
        booking.AttachPaymentIntent("pi_prefill");
        booking.ConfirmPayment("card", Now);
        await SeedAsync(booking);

        await using var ctx = _fixture.CreateContext();
        var detail = await Read(ctx).GetDetailAsync(booking.Id.Value, CancellationToken.None);

        detail!.SuggestedRefundPct.Should().Be(100);
        detail.SuggestedRefundAmount.Should().Be(300m);
    }

    [Fact]
    public async Task CheckIn_advances_a_confirmed_booking_but_rejects_an_awaiting_one()
    {
        var confirmed = Booking($"CI{Guid.NewGuid():N}"[..12], "c@example.com");
        confirmed.ConfirmPayment("card", Now);
        var awaiting = Booking($"AW{Guid.NewGuid():N}"[..12], "a@example.com");
        await SeedAsync(confirmed, awaiting);

        await using var ctx = _fixture.CreateContext();
        var svc = Service(ctx);

        (await svc.CheckInAsync(confirmed.Id.Value, CancellationToken.None)).Ok.Should().BeTrue();
        (await svc.CheckInAsync(awaiting.Id.Value, CancellationToken.None)).Ok.Should().BeFalse("AwaitingPayment can't check in");
        (await svc.CheckInAsync(Guid.NewGuid(), CancellationToken.None)).Ok.Should().BeFalse("unknown id");

        await using var verify = _fixture.CreateContext();
        (await Read(verify).GetDetailAsync(confirmed.Id.Value, CancellationToken.None))!.Status.Should().Be(BookingStatus.CheckedIn);
    }

    [Fact]
    public async Task Refund_without_a_stripe_payment_is_a_bookkeeping_mark_only()
    {
        var full = Booking($"RF{Guid.NewGuid():N}"[..12], "f@example.com");
        full.ConfirmPayment("cash", Now);
        var partial = Booking($"RP{Guid.NewGuid():N}"[..12], "p@example.com");
        partial.ConfirmPayment("cash", Now);
        await SeedAsync(full, partial);

        await using var ctx = _fixture.CreateContext();
        var refunds = new FakeRefunds();
        var svc = Service(ctx, refunds: refunds);

        (await svc.RefundAsync(full.Id.Value, partial: false, null, CancellationToken.None)).Ok.Should().BeTrue();
        (await svc.RefundAsync(partial.Id.Value, partial: true, 50m, CancellationToken.None)).Ok.Should().BeTrue();

        refunds.Calls.Should().BeEmpty("no Stripe payment exists, so no Stripe refund may be issued");
        await using var verify = _fixture.CreateContext();
        (await Read(verify).GetDetailAsync(full.Id.Value, CancellationToken.None))!.Status.Should().Be(BookingStatus.Refunded);
        (await Read(verify).GetDetailAsync(partial.Id.Value, CancellationToken.None))!.Status.Should().Be(BookingStatus.PartiallyRefunded);
    }

    [Fact]
    public async Task Refund_with_a_stripe_payment_issues_the_stripe_refund_and_marks_the_status()
    {
        var booking = Booking($"RS{Guid.NewGuid():N}"[..12], "rs@example.com");
        booking.AttachPaymentIntent("pi_refund_1");
        booking.ConfirmPayment("card", Now);
        await SeedAsync(booking);

        await using var ctx = _fixture.CreateContext();
        var refunds = new FakeRefunds();
        var svc = Service(ctx, refunds: refunds);

        (await svc.RefundAsync(booking.Id.Value, partial: true, 120m, CancellationToken.None)).Ok.Should().BeTrue();
        (await svc.RefundAsync(booking.Id.Value, partial: true, 9999m, CancellationToken.None)).Ok
            .Should().BeFalse("a refund above the booking total must be rejected");

        refunds.Calls.Should().ContainSingle().Which.Should().Be((booking.Id.Value, (decimal?)120m, "owner_refund"));
        await using var verify = _fixture.CreateContext();
        (await Read(verify).GetDetailAsync(booking.Id.Value, CancellationToken.None))!.Status.Should().Be(BookingStatus.PartiallyRefunded);
    }

    [Fact]
    public async Task Notes_and_sync_persist()
    {
        var booking = Booking($"NS{Guid.NewGuid():N}"[..12], "n@example.com");
        await SeedAsync(booking);

        await using var ctx = _fixture.CreateContext();
        var svc = Service(ctx);

        (await svc.SetNotesAsync(booking.Id.Value, "Late arrival ~23:00", CancellationToken.None)).Ok.Should().BeTrue();
        (await svc.MarkSyncedAsync(booking.Id.Value, "Mirrored in Hostify", CancellationToken.None)).Ok.Should().BeTrue();

        await using var verify = _fixture.CreateContext();
        var detail = await Read(verify).GetDetailAsync(booking.Id.Value, CancellationToken.None);
        detail!.Notes.Should().Be("Late arrival ~23:00");
        detail.ExternalChannelSyncedAtUtc.Should().NotBeNull();
        detail.ExternalChannelSyncNote.Should().Be("Mirrored in Hostify");
    }

    [Fact]
    public async Task Cancel_releases_the_dates_and_notifies_the_guest_and_property_manager()
    {
        var booking = Booking($"CX{Guid.NewGuid():N}"[..12], "x@example.com");
        await SeedAsync(booking);

        await using var ctx = _fixture.CreateContext();
        var lifecycle = new FakeLifecycle();
        var refunds = new FakeRefunds();
        var emails = new FakeEmails();
        var svc = Service(ctx, lifecycle: lifecycle, refunds: refunds, emails: emails);

        (await svc.CancelAsync(booking.Id.Value, "guest asked", 0m, CancellationToken.None)).Ok.Should().BeTrue();
        lifecycle.Cancelled.Should().ContainSingle().Which.Should().Be(booking.Id.Value);
        refunds.Calls.Should().BeEmpty("a €0 refund issues nothing");
        emails.Sent.Should().Contain((booking.Id.Value, BookingEmailKind.Cancellation))
            .And.Contain((booking.Id.Value, BookingEmailKind.PropertyManager));

        (await svc.CancelAsync(Guid.NewGuid(), null, 0m, CancellationToken.None)).Ok.Should().BeFalse("unknown id");
    }

    [Fact]
    public async Task Cancel_with_a_refund_issues_the_stripe_refund_first()
    {
        var booking = Booking($"CR{Guid.NewGuid():N}"[..12], "cr@example.com");
        booking.AttachPaymentIntent("pi_cancel_1");
        booking.ConfirmPayment("card", Now);
        await SeedAsync(booking);

        await using var ctx = _fixture.CreateContext();
        var lifecycle = new FakeLifecycle();
        var refunds = new FakeRefunds();
        var emails = new FakeEmails();
        var svc = Service(ctx, lifecycle: lifecycle, refunds: refunds, emails: emails);

        (await svc.CancelAsync(booking.Id.Value, "owner", 150m, CancellationToken.None)).Ok.Should().BeTrue();

        // The full Stage 7 Tests-list sentence: Stripe refund issued (mocked), dates released via the
        // lifecycle service, and the guest + property-manager alerts scheduled.
        refunds.Calls.Should().ContainSingle().Which.Should().Be((booking.Id.Value, (decimal?)150m, "owner_cancellation"));
        lifecycle.Cancelled.Should().ContainSingle();
        emails.Sent.Should().Contain((booking.Id.Value, BookingEmailKind.Cancellation))
            .And.Contain((booking.Id.Value, BookingEmailKind.PropertyManager));

        // Above the total, or positive without a Stripe payment → rejected before anything happens.
        (await svc.CancelAsync(booking.Id.Value, null, 9999m, CancellationToken.None)).Ok.Should().BeFalse();
        var manual = Booking($"CM{Guid.NewGuid():N}"[..12], "cm@example.com");
        manual.ConfirmPayment("cash", Now);
        await SeedAsync(manual);
        (await svc.CancelAsync(manual.Id.Value, null, 50m, CancellationToken.None)).Ok
            .Should().BeFalse("a positive refund needs a Stripe payment");
        refunds.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateManual_confirms_immediately_holds_the_nights_and_alerts()
    {
        await using var ctx = _fixture.CreateContext();
        var emails = new FakeEmails();
        var svc = Service(ctx, emails: emails);
        var checkIn = new DateOnly(2027, 3, 1);

        var result = await svc.CreateManualAsync(Manual(checkIn, checkIn.AddDays(3), "cash", null), CancellationToken.None);

        result.Ok.Should().BeTrue(result.Error);
        await using var verify = _fixture.CreateContext();
        var detail = await Read(verify).GetDetailAsync(result.BookingId!.Value, CancellationToken.None);
        detail!.Status.Should().Be(BookingStatus.Confirmed);
        detail.PaymentMethodType.Should().Be("cash");
        detail.StripePaymentIntentId.Should().BeNull();
        detail.Notes.Should().Be("walk-in");
        detail.Total.Should().Be(360m, "3 nights × €100 + €60 cleaning");

        var bookingId = BookingId.From(result.BookingId.Value);
        var nights = await verify.BookingDateOccupancies.Where(o => o.BookingId == bookingId).CountAsync();
        nights.Should().Be(3, "the occupancy rows make the dates unavailable and feed the ICS export");

        emails.Sent.Should().Contain((result.BookingId.Value, BookingEmailKind.Confirmation))
            .And.Contain((result.BookingId.Value, BookingEmailKind.OwnerNotification))
            .And.Contain((result.BookingId.Value, BookingEmailKind.PropertyManager));
    }

    [Fact]
    public async Task CreateManual_folds_the_agreed_amount_into_the_discount_line()
    {
        await using var ctx = _fixture.CreateContext();
        var svc = Service(ctx);
        var checkIn = new DateOnly(2027, 4, 1);

        // Quote total is 360 (3×100 + 60 cleaning); the owner agreed 300 on the phone.
        var result = await svc.CreateManualAsync(Manual(checkIn, checkIn.AddDays(3), "bank-transfer", 300m), CancellationToken.None);

        result.Ok.Should().BeTrue(result.Error);
        await using var verify = _fixture.CreateContext();
        var detail = await Read(verify).GetDetailAsync(result.BookingId!.Value, CancellationToken.None);
        detail!.Total.Should().Be(300m);
        detail.DiscountAmount.Should().Be(60m, "360 quoted − 300 agreed folds into the discount");
        detail.Subtotal.Should().Be(300m);
        detail.CleaningFee.Should().Be(60m);

        // Out-of-bounds amounts are rejected with a clear message.
        (await svc.CreateManualAsync(Manual(checkIn.AddDays(30), checkIn.AddDays(33), "cash", 10m), CancellationToken.None))
            .Ok.Should().BeFalse("€10 does not cover the €60 cleaning fee");
        (await svc.CreateManualAsync(Manual(checkIn.AddDays(60), checkIn.AddDays(63), "cash", 999m), CancellationToken.None))
            .Ok.Should().BeFalse("€999 exceeds the quoted total");
    }

    [Fact]
    public async Task CreateManual_rejects_overlapping_dates_and_bad_methods()
    {
        await using var ctx = _fixture.CreateContext();
        var svc = Service(ctx);
        var checkIn = new DateOnly(2027, 5, 1);

        (await svc.CreateManualAsync(Manual(checkIn, checkIn.AddDays(3), "cash", null), CancellationToken.None))
            .Ok.Should().BeTrue();
        var clash = await svc.CreateManualAsync(Manual(checkIn.AddDays(1), checkIn.AddDays(4), "cash", null), CancellationToken.None);
        clash.Ok.Should().BeFalse();
        clash.Error.Should().Contain("no longer available");

        (await svc.CreateManualAsync(Manual(checkIn.AddDays(30), checkIn.AddDays(32), "paypal", null), CancellationToken.None))
            .Ok.Should().BeFalse("paypal is not an allowed manual method");
        (await svc.CreateManualAsync(Manual(checkIn.AddDays(40), checkIn.AddDays(40), "cash", null), CancellationToken.None))
            .Ok.Should().BeFalse("check-out must be after check-in");
    }

    // ---------- helpers ----------

    private static ManualBookingCommand Manual(DateOnly checkIn, DateOnly checkOut, string method, decimal? amount) => new(
        checkIn, checkOut, Adults: 2, Children: 0, Infants: 0,
        GuestName: "Walk In", GuestEmail: "walkin@example.com", GuestPhone: "+351911111111",
        GuestCountry: "PT", GuestLanguage: "en",
        PaymentMethod: method, AmountReceivedEur: amount, SpecialRequests: null, Notes: "walk-in",
        SendGuestConfirmation: true);

    private static AdminBookingService Service(
        AppDbContext ctx,
        FakeLifecycle? lifecycle = null,
        FakeRefunds? refunds = null,
        FakeEmails? emails = null)
    {
        var quotes = new FakeQuotes();
        var creation = new BookingCreationService(
            ctx, quotes, new FakePricingReadStore(), new SequentialReferences(), new NoAvailability(),
            new NoIcsCache(), TimeProvider.System);
        return new AdminBookingService(
            ctx, lifecycle ?? new FakeLifecycle(), creation, quotes, refunds ?? new FakeRefunds(),
            emails ?? new FakeEmails(), TimeProvider.System);
    }

    private static AdminBookingReadService Read(AppDbContext ctx) => new(
        ctx, new FakePricingReadStore(), Options.Create(new StripeOptions()), TimeProvider.System);

    private static Booking Booking(string reference, string email, DateOnly? checkIn = null)
    {
        var start = checkIn ?? new DateOnly(2026, 6, 1);
        return new(
            BookingId.New(), reference, start, start.AddDays(3),
            adults: 2, children: 0, infants: 0,
            guestName: "Guest", guestEmail: email, guestPhone: "+351000000000", guestCountry: "PT", guestLanguage: "en",
            nightlyRateSnapshot: new Money(100m, "EUR"), subtotal: new Money(300m, "EUR"), discountAmount: new Money(0m, "EUR"),
            cleaningFee: new Money(0m, "EUR"), touristTax: new Money(0m, "EUR"), total: new Money(300m, "EUR"),
            createdAtUtc: Now);
    }

    private async Task SeedAsync(params Booking[] bookings)
    {
        await using var ctx = _fixture.CreateContext();
        ctx.Bookings.AddRange(bookings);
        await ctx.SaveChangesAsync();
    }

    private sealed class FakeLifecycle : IBookingLifecycleService
    {
        public List<Guid> Cancelled { get; } = [];

        public Task CancelAndReleaseAsync(BookingId bookingId, string? reason, CancellationToken cancellationToken)
        {
            Cancelled.Add(bookingId.Value);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRefunds : IRefundService
    {
        public List<(Guid BookingId, decimal? Amount, string Reason)> Calls { get; } = [];

        public Task RefundAsync(BookingId bookingId, decimal? amountEur, string reason, CancellationToken cancellationToken)
        {
            Calls.Add((bookingId.Value, amountEur, reason));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmails : IEmailJobScheduler
    {
        public List<(Guid BookingId, BookingEmailKind Kind)> Sent { get; } = [];

        public Task EnqueueAsync(Guid bookingId, BookingEmailKind kind, CancellationToken cancellationToken)
        {
            Sent.Add((bookingId, kind));
            return Task.CompletedTask;
        }
    }

    /// <summary>€100/night + €60 cleaning, no discount/tax — keeps the manual-booking math easy to assert.</summary>
    private sealed class FakeQuotes : IQuoteService
    {
        public Task<QuoteBreakdown> QuoteAsync(QuoteRequest request, CancellationToken cancellationToken)
        {
            var nights = request.CheckOut.DayNumber - request.CheckIn.DayNumber;
            var nightly = Enumerable.Range(0, Math.Max(nights, 0))
                .Select(i => new NightlyCharge(request.CheckIn.AddDays(i), new Money(100m, "EUR")))
                .ToList();
            var subtotal = new Money(100m * nights, "EUR");
            return Task.FromResult(new QuoteBreakdown(
                nights, nightly, subtotal,
                new DiscountLine(DiscountKind.None, 0, new Money(0m, "EUR")),
                new Money(60m, "EUR"), new Money(0m, "EUR"),
                new Money(subtotal.Amount + 60m, "EUR"), 1, false));
        }
    }

    private sealed class FakePricingReadStore : IPricingReadStore
    {
        public Task<IReadOnlyList<PricingRule>> GetPricingRulesAsync(DateOnly checkIn, DateOnly checkOut, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PricingRule>>([]);

        public Task<IReadOnlyList<Fee>> GetFeesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Fee>>([]);

        public Task<PromoCode?> GetPromoCodeAsync(string code, CancellationToken cancellationToken) =>
            Task.FromResult<PromoCode?>(null);

        public Task<CancellationPolicy?> GetCancellationPolicyAsync(CancellationToken cancellationToken) =>
            Task.FromResult<CancellationPolicy?>(new CancellationPolicy(
                CancellationPolicyId.New(), [new CancellationTier(14, 100), new CancellationTier(7, 50)]));

        public Task<IReadOnlyList<DailyRate>> GetDailyRatesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DailyRate>>([]);
    }

    private sealed class SequentialReferences : IBookingReferenceGenerator
    {
        public string Next() => $"GSM{Guid.NewGuid():N}"[..12].ToUpperInvariant();
    }

    private sealed class NoAvailability : IAvailabilityService
    {
        public Task<IReadOnlyList<DateOnly>> GetBlockedDatesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DateOnly>>([]);

        public void Invalidate()
        {
        }
    }

    private sealed class NoIcsCache : GaiaSkyline.Application.Availability.IIcsCacheInvalidator
    {
        public void Invalidate()
        {
        }
    }
}
