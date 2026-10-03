using GaiaSkyline.Domain.Bookings;

namespace GaiaSkyline.Application.Bookings;

/// <summary>Read model for the confirmation page and status poll. Amounts are in euros.</summary>
public sealed record BookingSummaryDto(
    string ReferenceCode,
    BookingStatus Status,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Adults,
    int Children,
    int Infants,
    string GuestName,
    string GuestLanguage,
    decimal Total,
    string? PaymentMethodType,
    string? MultibancoEntity,
    string? MultibancoReference,
    DateTime? PaymentExpiresAtUtc);

/// <summary>Reads a single booking by its human reference (confirmation page / status poll).</summary>
public interface IBookingReadStore
{
    Task<BookingSummaryDto?> GetByReferenceAsync(string referenceCode, CancellationToken cancellationToken);
}
