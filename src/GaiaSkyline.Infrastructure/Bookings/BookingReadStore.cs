using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>EF Core read model for bookings (untracked).</summary>
internal sealed class BookingReadStore(AppDbContext dbContext) : IBookingReadStore
{
    public async Task<BookingSummaryDto?> GetByReferenceAsync(string referenceCode, CancellationToken cancellationToken)
    {
        var normalized = referenceCode.Trim().ToUpperInvariant();
        var booking = await dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.ReferenceCode == normalized, cancellationToken);

        return booking is null ? null : Map(booking);
    }

    public async Task<IReadOnlyList<BookingSummaryDto>> GetForGuestEmailAsync(string email, CancellationToken cancellationToken)
    {
        // SQL Server's default collation is case-insensitive, so a direct compare matches any casing.
        var trimmed = email.Trim();
        var bookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(b => b.GuestEmail == trimmed)
            .OrderByDescending(b => b.CheckIn)
            .ToListAsync(cancellationToken);

        return bookings.Select(Map).ToList();
    }

    private static BookingSummaryDto Map(Booking booking) => new(
        booking.Id.Value,
        booking.ReferenceCode,
        booking.Status,
        booking.CheckIn,
        booking.CheckOut,
        booking.Nights,
        booking.Adults,
        booking.Children,
        booking.Infants,
        booking.GuestName,
        booking.GuestEmail,
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
