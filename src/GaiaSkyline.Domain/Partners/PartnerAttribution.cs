using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Partners;

public enum AttributionSource
{
    /// <summary>The 30-day first-party <c>gs_ref</c> cookie set by a referral link.</summary>
    Cookie,

    /// <summary>The partner's promo code typed at checkout — wins over the cookie (ADR 0019).</summary>
    Code,
}

/// <summary>
/// Which partner drove a booking, recorded at booking creation (Stage 8 Part A, ADR 0019). At most one row
/// per booking; a promo code typed at checkout wins over a referral cookie.
/// </summary>
public sealed class PartnerAttribution : Entity<PartnerAttributionId>
{
    // Required by EF Core's materialization.
    private PartnerAttribution()
    {
    }

    public PartnerAttribution(
        PartnerAttributionId id,
        PartnerId partnerId,
        BookingId bookingId,
        AttributionSource source,
        PromoCodeId? promoCodeId,
        DateTime createdAtUtc)
    {
        if (source == AttributionSource.Code && promoCodeId is null)
        {
            throw new ArgumentException("A code attribution must reference the promo code.", nameof(promoCodeId));
        }

        Id = id;
        PartnerId = partnerId;
        BookingId = bookingId;
        Source = source;
        PromoCodeId = promoCodeId;
        CreatedAtUtc = createdAtUtc;
    }

    public PartnerId PartnerId { get; private set; }

    public BookingId BookingId { get; private set; }

    public AttributionSource Source { get; private set; }

    public PromoCodeId? PromoCodeId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}
