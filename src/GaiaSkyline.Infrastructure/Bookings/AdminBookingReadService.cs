using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Application.Payments;
using GaiaSkyline.Application.Pricing;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>EF Core reads for the admin bookings manager (untracked). Stage 7 §6.</summary>
internal sealed class AdminBookingReadService(
    AppDbContext dbContext,
    IPricingReadStore pricingReadStore,
    IOptions<StripeOptions> stripeOptions,
    TimeProvider clock) : IAdminBookingReadService
{
    public async Task<IReadOnlyList<BookingAdminListItemDto>> GetAsync(BookingAdminFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.Bookings.AsNoTracking();

        if (filter.Status is { } status)
        {
            query = query.Where(b => b.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            // SQL Server's default collation is case-insensitive, so a plain Contains matches any casing.
            query = query.Where(b =>
                b.ReferenceCode.Contains(term) || b.GuestName.Contains(term) || b.GuestEmail.Contains(term));
        }

        if (filter.CheckInFrom is { } from)
        {
            query = query.Where(b => b.CheckIn >= from);
        }

        if (filter.CheckInTo is { } to)
        {
            query = query.Where(b => b.CheckIn <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.PaymentMethod))
        {
            query = query.Where(b => b.PaymentMethodType == filter.PaymentMethod);
        }

        if (filter.Synced is { } synced)
        {
            query = synced
                ? query.Where(b => b.ExternalChannelSyncedAtUtc != null)
                : query.Where(b => b.ExternalChannelSyncedAtUtc == null);
        }

        var bookings = await query
            .OrderByDescending(b => b.CheckIn)
            .ThenByDescending(b => b.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return bookings.Select(b => new BookingAdminListItemDto(
            b.Id.Value,
            b.ReferenceCode,
            b.Status,
            b.CheckIn,
            b.CheckOut,
            b.Nights,
            b.Adults,
            b.Children,
            b.Infants,
            b.GuestName,
            b.GuestEmail,
            b.Total.Amount,
            b.PaymentMethodType,
            b.ExternalChannelSyncedAtUtc is not null,
            b.CreatedAtUtc)).ToList();
    }

    public async Task<BookingAdminDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var bookingId = BookingId.From(id);
        var b = await dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);
        if (b is null)
        {
            return null;
        }

        var allowed = Enum.GetValues<BookingStatus>()
            .Where(to => BookingStatusTransitions.CanTransition(b.Status, to))
            .ToList();

        var (suggestedPct, suggestedAmount) = await SuggestRefundAsync(b, cancellationToken);

        return new BookingAdminDetailDto(
            b.Id.Value,
            b.ReferenceCode,
            b.Status,
            allowed,
            b.CheckIn,
            b.CheckOut,
            b.Nights,
            b.Adults,
            b.Children,
            b.Infants,
            b.GuestName,
            b.GuestEmail,
            b.GuestPhone,
            b.GuestCountry,
            b.GuestLanguage,
            b.NightlyRateSnapshot.Amount,
            b.Subtotal.Amount,
            b.DiscountAmount.Amount,
            b.CleaningFee.Amount,
            b.TouristTax.Amount,
            b.Total.Amount,
            b.PaymentMethodType,
            b.StripeCustomerId,
            b.StripePaymentIntentId,
            b.MultibancoEntity,
            b.MultibancoReference,
            b.PaymentExpiresAtUtc,
            b.ArrivalEstimateLocal,
            b.SpecialRequests,
            b.Notes,
            b.AccountCreationRequested,
            b.CreatedAtUtc,
            b.ConfirmedAtUtc,
            b.CancelledAtUtc,
            b.CancellationReason,
            b.ExternalChannelSyncedAtUtc,
            b.ExternalChannelSyncNote,
            StripeDashboardUrl(b.StripePaymentIntentId),
            suggestedPct,
            suggestedAmount);
    }

    /// <summary>
    /// The cancellation-policy refund for cancelling today — the prefill for the admin cancel form.
    /// Only a paid (Confirmed) Stripe booking gets a suggestion; everything else prefills €0.
    /// </summary>
    private async Task<(int Pct, decimal Amount)> SuggestRefundAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (booking.Status != BookingStatus.Confirmed || string.IsNullOrEmpty(booking.StripePaymentIntentId))
        {
            return (0, 0m);
        }

        var policy = await pricingReadStore.GetCancellationPolicyAsync(cancellationToken);
        var days = booking.CheckIn.DayNumber - LisbonClock.Today(clock).DayNumber;
        var pct = policy?.RefundPercentageFor(days) ?? 0;
        var amount = Math.Round(booking.Total.Amount * pct / 100m, 2, MidpointRounding.AwayFromZero);
        return (pct, amount);
    }

    /// <summary>Deep link to the payment in the Stripe dashboard (test-mode URL for a test key).</summary>
    private string? StripeDashboardUrl(string? paymentIntentId)
    {
        if (string.IsNullOrEmpty(paymentIntentId))
        {
            return null;
        }

        var secretKey = stripeOptions.Value.SecretKey;
        var isLive = secretKey.Contains("_live_", StringComparison.Ordinal);
        return isLive
            ? $"https://dashboard.stripe.com/payments/{paymentIntentId}"
            : $"https://dashboard.stripe.com/test/payments/{paymentIntentId}";
    }
}
