using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>EF Core reads for the admin bookings manager (untracked). Stage 7 §6.</summary>
internal sealed class AdminBookingReadService(AppDbContext dbContext) : IAdminBookingReadService
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
            b.ExternalChannelSyncNote);
    }
}
