namespace GaiaSkyline.Application.Partners;

/// <summary>
/// Referral attribution (Stage 8 Part A, ADR 0019). Clicks are recorded when a public URL arrives with
/// <c>?ref=CODE</c>; bookings are attributed at creation — a promo code typed at checkout wins over the
/// 30-day referral cookie. Suspended partners and unknown codes attribute nothing.
/// </summary>
public interface IPartnerAttributionService
{
    /// <summary>
    /// Records a referral click for a partner code and returns true when the code belongs to an active
    /// partner (the caller then sets the gs_ref cookie). <paramref name="landingPath"/> excludes <c>ref</c>.
    /// </summary>
    Task<bool> RecordClickAsync(string code, string landingPath, Guid anonymousId, CancellationToken cancellationToken);

    /// <summary>
    /// Attributes a just-created booking: <paramref name="typedPromoCode"/> (checkout) wins over
    /// <paramref name="referralCookieCode"/>. No-op when neither maps to an active partner.
    /// </summary>
    Task AttributeBookingAsync(
        Guid bookingId, string? typedPromoCode, string? referralCookieCode, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the commission for a confirmed, attributed booking (idempotent; one commission per booking).
    /// Skipped when the guest email equals the partner's email — the self-referral guard.
    /// </summary>
    Task OnBookingConfirmedAsync(Guid bookingId, CancellationToken cancellationToken);
}
