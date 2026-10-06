using GaiaSkyline.Application.Partners;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Partners;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>
/// Referral attribution (Stage 8 Part A, ADRs 0019/0020): clicks, the one-attribution-per-booking rule with
/// code-beats-cookie precedence, and commission creation on confirmation with the self-referral guard.
/// </summary>
internal sealed partial class PartnerAttributionService(
    AppDbContext dbContext,
    TimeProvider clock,
    ILogger<PartnerAttributionService> logger) : IPartnerAttributionService
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "No commission for booking {Reference}: the guest is the partner (self-referral).")]
    private static partial void LogSelfReferral(ILogger logger, string reference);

    public async Task<bool> RecordClickAsync(
        string code, string landingPath, Guid anonymousId, CancellationToken cancellationToken)
    {
        var partner = await ActivePartnerByCodeAsync(code, cancellationToken);
        if (partner is null)
        {
            return false;
        }

        dbContext.PartnerClicks.Add(new PartnerClick(
            PartnerClickId.New(), partner.Id,
            string.IsNullOrWhiteSpace(landingPath) ? "/" : landingPath,
            clock.GetUtcNow().UtcDateTime, anonymousId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task AttributeBookingAsync(
        Guid bookingId, string? typedPromoCode, string? referralCookieCode, CancellationToken cancellationToken)
    {
        var id = BookingId.From(bookingId);
        if (await dbContext.PartnerAttributions.AsNoTracking().AnyAsync(a => a.BookingId == id, cancellationToken))
        {
            return;
        }

        // A code typed at checkout wins over the cookie (ADR 0019). A typed code that is not a partner
        // code (a plain promo) falls through to the cookie.
        var source = AttributionSource.Code;
        var partner = await ActivePartnerByCodeAsync(typedPromoCode, cancellationToken);
        if (partner is null)
        {
            source = AttributionSource.Cookie;
            partner = await ActivePartnerByCodeAsync(referralCookieCode, cancellationToken);
        }

        if (partner is null)
        {
            return;
        }

        dbContext.PartnerAttributions.Add(new PartnerAttribution(
            PartnerAttributionId.New(), partner.Id, id, source,
            source == AttributionSource.Code ? partner.PromoCodeId : null,
            clock.GetUtcNow().UtcDateTime));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent attribution for the same booking won the unique index — nothing to do.
            dbContext.ChangeTracker.Clear();
        }
    }

    public async Task OnBookingConfirmedAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var id = BookingId.From(bookingId);
        var attribution = await dbContext.PartnerAttributions.AsNoTracking()
            .FirstOrDefaultAsync(a => a.BookingId == id, cancellationToken);
        if (attribution is null
            || await dbContext.Commissions.AsNoTracking().AnyAsync(c => c.BookingId == id, cancellationToken))
        {
            return;
        }

        var booking = await dbContext.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        var attributedPartnerId = attribution.PartnerId;
        var partner = await dbContext.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == attributedPartnerId, cancellationToken);

        // CheckedIn/Completed imply an earlier confirmation — the daily self-heal passes those in when the
        // confirmation-time hook was missed (ADR 0020).
        if (booking is null || partner is null
            || booking.Status is not (BookingStatus.Confirmed or BookingStatus.CheckedIn or BookingStatus.Completed))
        {
            return;
        }

        // Self-referral guard (ADR 0020): partners earn nothing on their own stays.
        if (string.Equals(booking.GuestEmail.Trim(), partner.Email, StringComparison.OrdinalIgnoreCase))
        {
            LogSelfReferral(logger, booking.ReferenceCode);
            return;
        }

        var basis = booking.Total - booking.TouristTax - booking.CleaningFee - booking.RefundedAmount;
        dbContext.Commissions.Add(new Commission(
            CommissionId.New(), partner.Id, id, basis, partner.CommissionPct, clock.GetUtcNow().UtcDateTime));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The daily self-heal or a concurrent webhook retry already created it.
            dbContext.ChangeTracker.Clear();
        }
    }

    private async Task<Partner?> ActivePartnerByCodeAsync(string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = code.Trim().ToUpperInvariant();
        var promo = await dbContext.PromoCodes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == normalized && p.PartnerId != null, cancellationToken);
        if (promo is null)
        {
            return null;
        }

        var partnerId = promo.PartnerId!.Value;
        var partner = await dbContext.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partnerId, cancellationToken);
        return partner is { Status: PartnerStatus.Active } ? partner : null;
    }
}
