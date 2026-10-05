using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Pricing;

/// <summary>
/// A percentage discount code. <see cref="PartnerId"/> stays null until Stage 8 (influencer
/// attribution). A code applies only when active and within its optional validity window.
/// </summary>
public sealed class PromoCode : Entity<PromoCodeId>
{
    // Required by EF Core's materialization.
    private PromoCode()
    {
    }

    public PromoCode(
        PromoCodeId id,
        string code,
        int discountPct,
        bool isActive,
        DateOnly? validFrom = null,
        DateOnly? validUntil = null,
        PartnerId? partnerId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(discountPct);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(discountPct, 100);
        if (validFrom is { } from && validUntil is { } until && until < from)
        {
            throw new ArgumentException("Promo code validUntil must be on or after validFrom.", nameof(validUntil));
        }

        Id = id;
        Code = code.Trim().ToUpperInvariant();
        DiscountPct = discountPct;
        IsActive = isActive;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        PartnerId = partnerId;
    }

    /// <summary>Admin edit: discount, active flag and validity window (the code itself never changes).</summary>
    public void Update(int discountPct, bool isActive, DateOnly? validFrom, DateOnly? validUntil)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(discountPct);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(discountPct, 100);
        if (validFrom is { } from && validUntil is { } until && until < from)
        {
            throw new ArgumentException("Promo code validUntil must be on or after validFrom.", nameof(validUntil));
        }

        DiscountPct = discountPct;
        IsActive = isActive;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
    }

    /// <summary>The code as entered by guests, stored upper-cased for case-insensitive matching.</summary>
    public string Code { get; private set; } = null!;

    public int DiscountPct { get; private set; }

    public bool IsActive { get; private set; }

    public DateOnly? ValidFrom { get; private set; }

    public DateOnly? ValidUntil { get; private set; }

    /// <summary>Influencer/partner attribution; null until Stage 8.</summary>
    public PartnerId? PartnerId { get; private set; }

    /// <summary>Whether the code can be redeemed on the given date.</summary>
    public bool IsRedeemableOn(DateOnly date) =>
        IsActive
        && (ValidFrom is null || date >= ValidFrom)
        && (ValidUntil is null || date <= ValidUntil);
}
