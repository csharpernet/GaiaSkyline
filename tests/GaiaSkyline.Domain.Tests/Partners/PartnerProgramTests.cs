using FluentAssertions;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Tests.Partners;

/// <summary>The influencer-program aggregates (Stage 8 Part A, ADRs 0019–0021).</summary>
public class PartnerProgramTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static Partner CreatePartner() =>
        new(PartnerId.New(), PartnerApplicationId.New(), "Rita Marques", "RITA@Example.com ", 5, 10, Now);

    [Theory]
    [InlineData("PT50000201231234567890154", true)] // valid PT IBAN
    [InlineData("pt50 0002 0123 1234 5678 9015 4", true)] // spacing + casing normalise
    [InlineData("DE89370400440532013000", true)] // valid DE IBAN
    [InlineData("PT50000201231234567890155", false)] // checksum off by one
    [InlineData("PT50", false)] // too short
    [InlineData("1250000201231234567890154", false)] // digits where the country code belongs
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Iban_mod97_validation(string? input, bool expected)
    {
        Iban.IsValid(input, out var normalized).Should().Be(expected);
        if (expected)
        {
            normalized.Should().NotBeNull().And.NotContain(" ");
        }
    }

    [Fact]
    public void Partner_activates_only_with_terms_and_payout_details()
    {
        var partner = CreatePartner();
        partner.Email.Should().Be("rita@example.com", "the self-referral guard compares lower-cased emails");

        var activateTooEarly = () => partner.Activate(Guid.NewGuid(), Now);
        activateTooEarly.Should().Throw<InvalidOperationException>("terms are not accepted yet");

        partner.AcceptTerms("20261001T000000Z", Now);
        activateTooEarly.Should().Throw<InvalidOperationException>("payout details are missing");

        partner.SetPayoutDetails("PT50000201231234567890154", "Rita Marques", "123456789", "pt");
        partner.PayoutCountry.Should().Be("PT");

        var userId = Guid.NewGuid();
        partner.Activate(userId, Now);
        partner.Status.Should().Be(PartnerStatus.Active);
        partner.UserId.Should().Be(userId);

        partner.Suspend();
        partner.Status.Should().Be(PartnerStatus.Suspended);
        partner.Reactivate();
        partner.Status.Should().Be(PartnerStatus.Active);
    }

    [Fact]
    public void Bad_iban_is_rejected_with_a_friendly_error()
    {
        var partner = CreatePartner();
        var act = () => partner.SetPayoutDetails("PT50000201231234567890155", "Rita", "123456789", "PT");
        act.Should().Throw<ArgumentException>().WithMessage("*checksum*");
    }

    [Fact]
    public void Invite_is_single_use_and_expires_after_seven_days()
    {
        var invite = new PartnerInvite(PartnerInviteId.New(), PartnerId.New(), Now);

        invite.IsUsable(Now.AddDays(6)).Should().BeTrue();
        invite.IsUsable(Now.AddDays(8)).Should().BeFalse("the link lives for seven days");

        invite.Consume(Now.AddDays(1));
        invite.IsUsable(Now.AddDays(2)).Should().BeFalse("the link works exactly once");
        var again = () => invite.Consume(Now.AddDays(2));
        again.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Commission_amount_follows_the_basis_and_recalculates_until_paid()
    {
        var commission = new Commission(
            CommissionId.New(), PartnerId.New(), BookingId.New(),
            new Money(400m, "EUR"), 10, Now);

        commission.Amount.Should().Be(new Money(40m, "EUR"));
        commission.Status.Should().Be(CommissionStatus.Pending);

        // A partial refund shrinks the basis (ADR 0020); a negative basis floors at zero.
        commission.Recalculate(new Money(250m, "EUR"));
        commission.Amount.Should().Be(new Money(25m, "EUR"));
        commission.Recalculate(new Money(-10m, "EUR"));
        commission.Amount.Should().Be(new Money(0m, "EUR"));

        commission.MakePayable();
        commission.Status.Should().Be(CommissionStatus.Payable);

        var payout = PayoutId.New();
        commission.AssignToPayout(payout);
        commission.Status.Should().Be(CommissionStatus.Paid);
        commission.PayoutId.Should().Be(payout);

        var recalcPaid = () => commission.Recalculate(new Money(100m, "EUR"));
        recalcPaid.Should().Throw<InvalidOperationException>("a paid commission is final");
        var voidPaid = () => commission.Void();
        voidPaid.Should().Throw<InvalidOperationException>("a paid commission cannot be voided");
    }

    [Fact]
    public void Attribution_with_code_source_requires_the_promo_id()
    {
        var act = () => new PartnerAttribution(
            PartnerAttributionId.New(), PartnerId.New(), BookingId.New(),
            AttributionSource.Code, promoCodeId: null, Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Booking_accumulates_refunds_capped_at_the_total()
    {
        var booking = new GaiaSkyline.Domain.Bookings.Booking(
            BookingId.New(), "GS-REFUND1", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 4),
            2, 0, 0, "Guest", "g@example.com", "+351911111111", "PT", "en",
            new Money(100m, "EUR"), new Money(300m, "EUR"), Money.Zero("EUR"),
            new Money(60m, "EUR"), new Money(8m, "EUR"), new Money(368m, "EUR"), Now);

        booking.RefundedAmount.Should().Be(Money.Zero("EUR"));
        booking.RecordRefund(new Money(100m, "EUR"));
        booking.RecordRefund(new Money(300m, "EUR"));
        booking.RefundedAmount.Should().Be(new Money(368m, "EUR"), "refunds never exceed the total");
    }
}
