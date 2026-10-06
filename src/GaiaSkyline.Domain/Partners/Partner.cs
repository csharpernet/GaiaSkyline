using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Partners;

public enum PartnerStatus
{
    /// <summary>Approved and invited; the onboarding link has not been completed yet.</summary>
    Invited,

    /// <summary>Onboarded: account created, terms accepted, payout details on file.</summary>
    Active,

    /// <summary>Suspended by the owner: links stop attributing and the dashboard is read-only.</summary>
    Suspended,
}

/// <summary>
/// An influencer in the partner program (Stage 8 Part A): identity link, promo code, the percentages that
/// drive guest discounts and commissions, accepted terms, and payout details. Created when the owner approves
/// a <see cref="PartnerApplication"/>; activated when the partner completes the invite link.
/// </summary>
public sealed class Partner : Entity<PartnerId>
{
    // Required by EF Core's materialization.
    private Partner()
    {
    }

    public Partner(
        PartnerId id,
        PartnerApplicationId applicationId,
        string name,
        string email,
        int guestDiscountPct,
        int commissionPct,
        DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Id = id;
        ApplicationId = applicationId;
        Name = name.Trim();
        Email = email.Trim().ToLowerInvariant();
        SetPercentages(guestDiscountPct, commissionPct);
        Status = PartnerStatus.Invited;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>The approved application this partner was created from.</summary>
    public PartnerApplicationId ApplicationId { get; private set; }

    /// <summary>The Identity user once onboarding completes; null while only invited.</summary>
    public Guid? UserId { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Lower-cased contact email; also the self-referral guard key (no commission on own bookings).</summary>
    public string Email { get; private set; } = null!;

    /// <summary>The partner's promo code (guest-facing attribution + discount); set right after creation.</summary>
    public PromoCodeId? PromoCodeId { get; private set; }

    /// <summary>Guest discount the partner's code grants, percent of the nightly subtotal.</summary>
    public int GuestDiscountPct { get; private set; }

    /// <summary>The partner's commission, percent of the commission basis (ADR 0020).</summary>
    public int CommissionPct { get; private set; }

    public PartnerStatus Status { get; private set; }

    /// <summary>The partner-terms version accepted during onboarding (the terms content block's version stamp).</summary>
    public string? TermsVersion { get; private set; }

    public DateTime? TermsAcceptedAtUtc { get; private set; }

    public string? PayoutIban { get; private set; }

    public string? PayoutAccountHolder { get; private set; }

    /// <summary>NIF / tax identifier for the payout statement.</summary>
    public string? PayoutTaxId { get; private set; }

    public string? PayoutCountry { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ActivatedAtUtc { get; private set; }

    public void SetPromoCode(PromoCodeId promoCodeId) => PromoCodeId = promoCodeId;

    public void SetPercentages(int guestDiscountPct, int commissionPct)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(guestDiscountPct);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(guestDiscountPct, 100);
        ArgumentOutOfRangeException.ThrowIfNegative(commissionPct);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(commissionPct, 100);

        GuestDiscountPct = guestDiscountPct;
        CommissionPct = commissionPct;
    }

    /// <summary>Stores payout details; the IBAN must pass the mod-97 check (Stage 8 Part A).</summary>
    public void SetPayoutDetails(string iban, string accountHolder, string taxId, string country)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountHolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(taxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(country);

        if (!Iban.IsValid(iban, out var normalized))
        {
            throw new ArgumentException("That IBAN does not pass the checksum — check for typos.", nameof(iban));
        }

        PayoutIban = normalized;
        PayoutAccountHolder = accountHolder.Trim();
        PayoutTaxId = taxId.Trim();
        PayoutCountry = country.Trim().ToUpperInvariant();
    }

    public void AcceptTerms(string version, DateTime atUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        TermsVersion = version.Trim();
        TermsAcceptedAtUtc = atUtc;
    }

    /// <summary>Completes onboarding: links the Identity user. Requires accepted terms and payout details.</summary>
    public void Activate(Guid userId, DateTime atUtc)
    {
        if (Status != PartnerStatus.Invited)
        {
            throw new InvalidOperationException($"Only an invited partner can be activated (status: {Status}).");
        }

        if (TermsAcceptedAtUtc is null)
        {
            throw new InvalidOperationException("The partner terms must be accepted before activation.");
        }

        if (PayoutIban is null)
        {
            throw new InvalidOperationException("Payout details must be on file before activation.");
        }

        UserId = userId;
        Status = PartnerStatus.Active;
        ActivatedAtUtc = atUtc;
    }

    public void Suspend()
    {
        if (Status != PartnerStatus.Active)
        {
            throw new InvalidOperationException($"Only an active partner can be suspended (status: {Status}).");
        }

        Status = PartnerStatus.Suspended;
    }

    public void Reactivate()
    {
        if (Status != PartnerStatus.Suspended)
        {
            throw new InvalidOperationException($"Only a suspended partner can be reactivated (status: {Status}).");
        }

        Status = PartnerStatus.Active;
    }
}
