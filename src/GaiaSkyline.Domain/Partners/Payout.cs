using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.ValueObjects;

namespace GaiaSkyline.Domain.Partners;

public enum PayoutStatus
{
    /// <summary>Statement issued; the owner has not transferred the money yet.</summary>
    Created,

    /// <summary>The owner transferred the amount and marked it settled (money moves manually, ADR 0021).</summary>
    Settled,
}

/// <summary>
/// One monthly payout to a partner (Stage 8 Part A, ADR 0021): the payable commissions grouped on the 5th,
/// a PDF statement, and a manual settlement step — the system never moves money itself.
/// </summary>
public sealed class Payout : Entity<PayoutId>
{
    // Required by EF Core's materialization.
    private Payout()
    {
    }

    public Payout(PayoutId id, PartnerId partnerId, string periodLabel, Money amount, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(periodLabel);
        if (amount.Amount <= 0)
        {
            throw new ArgumentException("A payout must be a positive amount.", nameof(amount));
        }

        Id = id;
        PartnerId = partnerId;
        PeriodLabel = periodLabel.Trim();
        Amount = amount;
        Status = PayoutStatus.Created;
        CreatedAtUtc = createdAtUtc;
    }

    public PartnerId PartnerId { get; private set; }

    /// <summary>The period the statement covers, e.g. "2026-10" (the run on the 5th of November).</summary>
    public string PeriodLabel { get; private set; } = null!;

    public Money Amount { get; private set; }

    public PayoutStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? SettledAtUtc { get; private set; }

    public void MarkSettled(DateTime atUtc)
    {
        if (Status != PayoutStatus.Created)
        {
            throw new InvalidOperationException("This payout is already settled.");
        }

        Status = PayoutStatus.Settled;
        SettledAtUtc = atUtc;
    }
}
