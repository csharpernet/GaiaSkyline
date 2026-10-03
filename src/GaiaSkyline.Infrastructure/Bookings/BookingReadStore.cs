using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>EF Core read model for a single booking by reference (untracked).</summary>
internal sealed class BookingReadStore(AppDbContext dbContext) : IBookingReadStore
{
    public async Task<BookingSummaryDto?> GetByReferenceAsync(string referenceCode, CancellationToken cancellationToken)
    {
        var normalized = referenceCode.Trim().ToUpperInvariant();
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.ReferenceCode == normalized, cancellationToken);
        if (booking is null)
        {
            return null;
        }

        return new BookingSummaryDto(
            booking.ReferenceCode,
            booking.Status,
            booking.CheckIn,
            booking.CheckOut,
            booking.Nights,
            booking.Adults,
            booking.Children,
            booking.Infants,
            booking.GuestName,
            booking.GuestLanguage,
            booking.Subtotal.Amount,
            booking.DiscountAmount.Amount,
            booking.CleaningFee.Amount,
            booking.TouristTax.Amount,
            booking.Total.Amount,
            booking.PaymentMethodType,
            booking.MultibancoEntity,
            booking.MultibancoReference,
            booking.PaymentExpiresAtUtc);
    }
}
