using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Partners;

public enum CommissionStatus
{
    /// <summary>Earned on confirmation; waiting out the 30-days-after-check-out safety window.</summary>
    Pending,

    /// <summary>Cleared the window; picked up by the next monthly payout run.</summary>
    Payable,

    /// <summary>Included in a <see cref="Payout"/>.</summary>
    Paid,

    /// <summary>Cancelled booking or full refund — nothing is owed.</summary>
    Void,
}

/// <summary>
/// A partner's earnings on one attributed booking (Stage 8 Part A, ADR 0020). The basis is
/// Total − TouristTax − CleaningFee − refunded amounts; the amount is the partner's percentage of it,
/// recalculated while the commission is still unpaid when a partial refund lands.
/// </summary>
public sealed class Commission : Entity<CommissionId>
{
    // Required by EF Core's materialization.
    private Commission()
    {
    }

    public Commission(
        CommissionId id,
        PartnerId partnerId,
        BookingId bookingId,
        Money basisAmount,
        int pct,
        DateTime createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pct);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pct, 100);

        Id = id;
        PartnerId = partnerId;
        BookingId = bookingId;
        Pct = pct;
        Status = CommissionStatus.Pending;
        CreatedAtUtc = createdAtUtc;
        ApplyBasis(basisAmount);
    }

    public PartnerId PartnerId { get; private set; }

    public BookingId BookingId { get; private set; }

    /// <summary>Total − TouristTax − CleaningFee − refunds, never below zero (ADR 0020).</summary>
    public Money BasisAmount { get; private set; }

    public int Pct { get; private set; }

    /// <summary>The partner's share: basis × pct, rounded to cents away from zero.</summary>
    public Money Amount { get; private set; }

    public CommissionStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>The payout that paid this commission out; null until then.</summary>
    public PayoutId? PayoutId { get; private set; }

    /// <summary>A partial refund shrinks the basis while the commission is still unpaid (ADR 0020).</summary>
    public void Recalculate(Money basisAmount)
    {
        if (Status is CommissionStatus.Paid or CommissionStatus.Void)
        {
            throw new InvalidOperationException($"A {Status} commission is final and cannot be recalculated.");
        }

        ApplyBasis(basisAmount);
    }

    public void MakePayable()
    {
        if (Status != CommissionStatus.Pending)
        {
            throw new InvalidOperationException($"Only a pending commission can become payable (status: {Status}).");
        }

        Status = CommissionStatus.Payable;
    }

    /// <summary>Cancellation or full refund — nothing is owed. Paid commissions stay paid.</summary>
    public void Void()
    {
        if (Status == CommissionStatus.Paid)
        {
            throw new InvalidOperationException("A paid commission cannot be voided.");
        }

        Status = CommissionStatus.Void;
    }

    public void AssignToPayout(PayoutId payoutId)
    {
        if (Status != CommissionStatus.Payable)
        {
            throw new InvalidOperationException($"Only a payable commission can be paid out (status: {Status}).");
        }

        PayoutId = payoutId;
        Status = CommissionStatus.Paid;
    }

    private void ApplyBasis(Money basisAmount)
    {
        var basis = basisAmount.Amount < 0 ? Money.Zero(basisAmount.Currency) : basisAmount;
        BasisAmount = basis;
        Amount = new Money(
            Math.Round(basis.Amount * Pct / 100m, 2, MidpointRounding.AwayFromZero), basis.Currency);
    }
}
