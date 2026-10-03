using GaiaSkyline.Domain.Bookings;

namespace GaiaSkyline.Application.Bookings;

/// <summary>Read model for the confirmation page, status poll and guest area. Amounts are in euros.</summary>
public sealed record BookingSummaryDto(
    Guid Id,
    string ReferenceCode,
    BookingStatus Status,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Adults,
    int Children,
    int Infants,
    string GuestName,
    string GuestEmail,
    string GuestLanguage,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal CleaningFee,
    decimal TouristTax,
    decimal Total,
    string? PaymentMethodType,
    string? MultibancoEntity,
    string? MultibancoReference,
    DateTime? PaymentExpiresAtUtc);

/// <summary>Reads bookings for the confirmation page, status poll and the signed-in guest area.</summary>
public interface IBookingReadStore
{
    Task<BookingSummaryDto?> GetByReferenceAsync(string referenceCode, CancellationToken cancellationToken);

    /// <summary>All bookings for a guest email (most recent first), for the /my/bookings list.</summary>
    Task<IReadOnlyList<BookingSummaryDto>> GetForGuestEmailAsync(string email, CancellationToken cancellationToken);
}
