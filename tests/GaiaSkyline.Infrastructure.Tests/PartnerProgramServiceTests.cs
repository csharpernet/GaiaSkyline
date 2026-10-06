using FluentAssertions;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Application.Settings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Domain.Pricing;
using GaiaSkyline.Domain.ValueObjects;
using GaiaSkyline.Infrastructure.Partners;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>
/// Attribution, commission lifecycle and payouts (Stage 8 Part A, ADRs 0019–0021): code beats cookie, the
/// self-referral guard, the 30-day payable window, void/recalculate on refunds, and the monthly payout run
/// with the €50 minimum, idempotency per period and the PDF statement.
/// </summary>
public sealed class PartnerProgramServiceTests : IClassFixture<LocalDbFixture>
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly LocalDbFixture _fixture;

    public PartnerProgramServiceTests(LocalDbFixture fixture)
    {
        _fixture = fixture;
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    }

    [Fact]
    public async Task Click_records_only_for_active_partners_and_attribution_prefers_the_typed_code()
    {
        await using var ctx = _fixture.CreateContext();
        var (active, activeCode) = await SeedPartnerAsync(ctx, "Active One");
        var (suspended, suspendedCode) = await SeedPartnerAsync(ctx, "Suspended One");
        suspended.Suspend();
        await ctx.SaveChangesAsync();

        var service = Attribution(ctx);

        (await service.RecordClickAsync(activeCode, "/en", Guid.NewGuid(), CancellationToken.None))
            .Should().BeTrue();
        (await service.RecordClickAsync(suspendedCode, "/en", Guid.NewGuid(), CancellationToken.None))
            .Should().BeFalse("a suspended partner's code attributes nothing");
        (await service.RecordClickAsync("NOT-A-CODE", "/en", Guid.NewGuid(), CancellationToken.None))
            .Should().BeFalse();

        var activeId = active.Id;
        (await ctx.PartnerClicks.CountAsync(c => c.PartnerId == activeId)).Should().Be(1);

        // Typed code wins over the cookie (ADR 0019); the cookie attributes when no partner code was typed.
        var byCode = await SeedBookingAsync(ctx, "guest1@example.com");
        await service.AttributeBookingAsync(byCode.Id.Value, activeCode, suspendedCode, CancellationToken.None);
        var byCookie = await SeedBookingAsync(ctx, "guest2@example.com");
        await service.AttributeBookingAsync(byCookie.Id.Value, "PLAINPROMO", activeCode, CancellationToken.None);
        var unattributed = await SeedBookingAsync(ctx, "guest3@example.com");
        await service.AttributeBookingAsync(unattributed.Id.Value, null, suspendedCode, CancellationToken.None);

        var codeRowBookingId = byCode.Id;
        var codeRow = await ctx.PartnerAttributions.SingleAsync(a => a.BookingId == codeRowBookingId);
        codeRow.Source.Should().Be(AttributionSource.Code);
        codeRow.PartnerId.Should().Be(active.Id);

        var cookieRowBookingId = byCookie.Id;
        var cookieRow = await ctx.PartnerAttributions.SingleAsync(a => a.BookingId == cookieRowBookingId);
        cookieRow.Source.Should().Be(AttributionSource.Cookie);

        var unattributedId = unattributed.Id;
        (await ctx.PartnerAttributions.AnyAsync(a => a.BookingId == unattributedId))
            .Should().BeFalse("a suspended partner's cookie attributes nothing");
    }

    [Fact]
    public async Task Confirmation_creates_the_commission_on_the_adr_basis_and_guards_self_referral()
    {
        await using var ctx = _fixture.CreateContext();
        var (partner, code) = await SeedPartnerAsync(ctx, "Basis Check");
        var service = Attribution(ctx);

        // Basis = total − tourist tax − cleaning − refunds (ADR 0020): 368 − 8 − 60 = 300 → 10% = 30.
        var booking = await SeedBookingAsync(ctx, "basis-guest@example.com");
        booking.ConfirmPayment("card", Now);
        await ctx.SaveChangesAsync();
        await service.AttributeBookingAsync(booking.Id.Value, code, null, CancellationToken.None);

        await service.OnBookingConfirmedAsync(booking.Id.Value, CancellationToken.None);
        await service.OnBookingConfirmedAsync(booking.Id.Value, CancellationToken.None); // idempotent

        var bookingId = booking.Id;
        var commission = await ctx.Commissions.SingleAsync(c => c.BookingId == bookingId);
        commission.BasisAmount.Should().Be(new Money(300m, "EUR"));
        commission.Amount.Should().Be(new Money(30m, "EUR"));
        commission.Status.Should().Be(CommissionStatus.Pending);

        // The partner books for themselves → no commission (same email as the partner).
        var self = await SeedBookingAsync(ctx, partner.Email);
        self.ConfirmPayment("card", Now);
        await ctx.SaveChangesAsync();
        await service.AttributeBookingAsync(self.Id.Value, code, null, CancellationToken.None);
        await service.OnBookingConfirmedAsync(self.Id.Value, CancellationToken.None);

        var selfId = self.Id;
        (await ctx.Commissions.AnyAsync(c => c.BookingId == selfId)).Should().BeFalse();
    }

    [Fact]
    public async Task Daily_run_moves_pending_to_payable_after_30_days_voids_cancellations_and_recalculates_refunds()
    {
        await using var ctx = _fixture.CreateContext();
        var (partner, code) = await SeedPartnerAsync(ctx, "Lifecycle One");
        var time = new TestTime { Now = Now };
        var attribution = Attribution(ctx);
        var emails = new RecordingEmailSender();
        var service = Commissions(ctx, attribution, emails, time, minPayout: "50");

        async Task<Booking> ConfirmedAttributedAsync(string email, DateOnly checkIn, DateOnly checkOut)
        {
            var b = await SeedBookingAsync(ctx, email, checkIn, checkOut);
            b.ConfirmPayment("card", time.Now.UtcDateTime);
            await ctx.SaveChangesAsync();
            await attribution.AttributeBookingAsync(b.Id.Value, code, null, CancellationToken.None);
            await attribution.OnBookingConfirmedAsync(b.Id.Value, CancellationToken.None);
            return b;
        }

        // Checked out long ago → payable; recent → stays pending; cancelled → void; refunded → recalculated.
        var payable = await ConfirmedAttributedAsync("p1@example.com", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 4));
        var fresh = await ConfirmedAttributedAsync("p2@example.com", new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 28));
        var cancelled = await ConfirmedAttributedAsync("p3@example.com", new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 8));
        cancelled.Cancel("guest asked", time.Now.UtcDateTime);
        var refunded = await ConfirmedAttributedAsync("p4@example.com", new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 13));
        refunded.RecordRefund(new Money(100m, "EUR"));
        refunded.MarkPartiallyRefunded();
        await ctx.SaveChangesAsync();

        await service.RunDailyAsync(CancellationToken.None);

        (await StatusOf(ctx, payable)).Should().Be(CommissionStatus.Payable);
        (await StatusOf(ctx, fresh)).Should().Be(CommissionStatus.Pending, "check-out is not 30 days past");
        (await StatusOf(ctx, cancelled)).Should().Be(CommissionStatus.Void);

        var refundedId = refunded.Id;
        var recalculated = await ctx.Commissions.AsNoTracking().SingleAsync(c => c.BookingId == refundedId);
        recalculated.BasisAmount.Should().Be(new Money(200m, "EUR"), "300 basis − 100 refund");
        recalculated.Amount.Should().Be(new Money(20m, "EUR"));
    }

    [Fact]
    public async Task Payout_run_groups_payables_respects_the_minimum_is_idempotent_and_emails_the_statement()
    {
        await using var ctx = _fixture.CreateContext();
        var (rich, richCode) = await SeedPartnerAsync(ctx, "Rich Partner");
        var (poor, poorCode) = await SeedPartnerAsync(ctx, "Poor Partner");
        var time = new TestTime { Now = Now };
        var attribution = Attribution(ctx);
        var emails = new RecordingEmailSender();
        var service = Commissions(ctx, attribution, emails, time, minPayout: "50");

        // Rich: two payable commissions of €30 each (≥ €50 together); poor: one €30 (below the minimum).
        foreach (var (email, c) in new[] { ("r1@e.com", richCode), ("r2@e.com", richCode), ("q1@e.com", poorCode) })
        {
            var b = await SeedBookingAsync(ctx, email, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 4));
            b.ConfirmPayment("card", time.Now.UtcDateTime);
            await ctx.SaveChangesAsync();
            await attribution.AttributeBookingAsync(b.Id.Value, c, null, CancellationToken.None);
            await attribution.OnBookingConfirmedAsync(b.Id.Value, CancellationToken.None);
        }

        await service.RunDailyAsync(CancellationToken.None); // everything checked out >30d ago → payable

        // The run sweeps the whole (shared-fixture) database, so assertions stay scoped to THESE partners.
        (await service.RunPayoutsAsync(CancellationToken.None)).Should().BeGreaterThanOrEqualTo(1);
        (await service.RunPayoutsAsync(CancellationToken.None)).Should().Be(0, "the period already ran");

        var poorPartnerId = poor.Id;
        (await ctx.Payouts.AsNoTracking().AnyAsync(p => p.PartnerId == poorPartnerId))
            .Should().BeFalse("€30 is below the €50 minimum");

        var richId = rich.Id;
        var payout = await ctx.Payouts.AsNoTracking().SingleAsync(p => p.PartnerId == richId);
        payout.Amount.Should().Be(new Money(60m, "EUR"));
        payout.PeriodLabel.Should().Be("2026-09", "the October run pays the previous month's label");
        payout.Status.Should().Be(PayoutStatus.Created);

        (await ctx.Commissions.AsNoTracking().CountAsync(c => c.PayoutId == payout.Id))
            .Should().Be(2, "both payable commissions joined the payout and are now Paid");
        var poorId = poor.Id;
        (await ctx.Commissions.AsNoTracking()
                .CountAsync(c => c.PartnerId == poorId && c.Status == CommissionStatus.Payable))
            .Should().Be(1, "below the minimum carries over");

        emails.Sent.Should().ContainSingle(m => m.ToAddress == rich.Email && m.Attachments != null
            && m.Attachments.Count == 1 && m.Attachments[0].ContentType == "application/pdf");

        var statement = await new PartnerStatementPdfService(ctx).GenerateAsync(payout.Id.Value, CancellationToken.None);
        statement.Should().NotBeNull();
        statement!.Length.Should().BeGreaterThan(1000, "a real PDF document is produced");
    }

    private static PartnerAttributionService Attribution(AppDbContext ctx) =>
        new(ctx, TimeProvider.System, NullLogger<PartnerAttributionService>.Instance);

    private static PartnerCommissionService Commissions(
        AppDbContext ctx, IPartnerAttributionService attribution, RecordingEmailSender emails,
        TimeProvider time, string minPayout) =>
        new(ctx, attribution, new PartnerStatementPdfService(ctx), emails,
            new StubSettings(new Dictionary<string, string?> { [SettingKeys.PartnerMinPayoutEur] = minPayout }),
            time, NullLogger<PartnerCommissionService>.Instance);

    private static async Task<CommissionStatus> StatusOf(AppDbContext ctx, Booking booking)
    {
        var id = booking.Id;
        return (await ctx.Commissions.AsNoTracking().SingleAsync(c => c.BookingId == id)).Status;
    }

    private static async Task<(Partner Partner, string Code)> SeedPartnerAsync(AppDbContext ctx, string name)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var partner = new Partner(
            PartnerId.New(), PartnerApplicationId.New(), name, $"{unique}@partner.example", 5, 10, Now);
        partner.AcceptTerms("test", Now);
        partner.SetPayoutDetails("PT50000201231234567890154", name, "123456789", "PT");
        partner.Activate(Guid.NewGuid(), Now);

        var code = $"TEST{unique.ToUpperInvariant()}";
        var promo = new PromoCode(PromoCodeId.New(), code, 5, isActive: true, partnerId: partner.Id);
        partner.SetPromoCode(promo.Id);

        ctx.Partners.Add(partner);
        ctx.PromoCodes.Add(promo);
        await ctx.SaveChangesAsync();
        return (partner, code);
    }

    private static async Task<Booking> SeedBookingAsync(
        AppDbContext ctx, string email, DateOnly? checkIn = null, DateOnly? checkOut = null)
    {
        // 300 subtotal + 60 cleaning + 8 tax = 368 total → ADR basis 300.
        var booking = new Booking(
            BookingId.New(), $"GS{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            checkIn ?? new DateOnly(2026, 11, 1), checkOut ?? new DateOnly(2026, 11, 4),
            2, 0, 0, "Guest Person", email, "+351911111111", "PT", "en",
            new Money(100m, "EUR"), new Money(300m, "EUR"), Money.Zero("EUR"),
            new Money(60m, "EUR"), new Money(8m, "EUR"), new Money(368m, "EUR"), Now);
        ctx.Bookings.Add(booking);
        await ctx.SaveChangesAsync();
        return booking;
    }

    private sealed class TestTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class StubSettings(Dictionary<string, string?> values) : ISiteSettings
    {
        public string? Get(string key) => values.GetValueOrDefault(key);

        public string? GetMasked(string key) => Get(key);

        public void Reload()
        {
        }
    }
}
