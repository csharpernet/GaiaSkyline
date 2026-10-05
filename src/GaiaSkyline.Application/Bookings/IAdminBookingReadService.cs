using GaiaSkyline.Domain.Bookings;

namespace GaiaSkyline.Application.Bookings;

/// <summary>Filter for the admin bookings list. Any null field is ignored. Stage 7 §6.</summary>
public sealed record BookingAdminFilter(
    BookingStatus? Status,
    string? Search,
    DateOnly? CheckInFrom,
    DateOnly? CheckInTo);

/// <summary>A row in the admin bookings list. Amounts are in euros.</summary>
public sealed record BookingAdminListItemDto(
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
    decimal Total,
    string? PaymentMethodType,
    bool ChannelSynced,
    DateTime CreatedAtUtc);

/// <summary>Everything the admin booking detail page shows, including the legal next statuses. Amounts in euros.</summary>
public sealed record BookingAdminDetailDto(
    Guid Id,
    string ReferenceCode,
    BookingStatus Status,
    IReadOnlyList<BookingStatus> AllowedTransitions,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Adults,
    int Children,
    int Infants,
    string GuestName,
    string GuestEmail,
    string GuestPhone,
    string GuestCountry,
    string GuestLanguage,
    decimal NightlyRate,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal CleaningFee,
    decimal TouristTax,
    decimal Total,
    string? PaymentMethodType,
    string? StripeCustomerId,
    string? StripePaymentIntentId,
    string? MultibancoEntity,
    string? MultibancoReference,
    DateTime? PaymentExpiresAtUtc,
    TimeOnly? ArrivalEstimateLocal,
    string? SpecialRequests,
    string? Notes,
    bool AccountCreationRequested,
    DateTime CreatedAtUtc,
    DateTime? ConfirmedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    DateTime? ExternalChannelSyncedAtUtc,
    string? ExternalChannelSyncNote);

/// <summary>Owner-only reads for the admin bookings manager (Stage 7 §6).</summary>
public interface IAdminBookingReadService
{
    Task<IReadOnlyList<BookingAdminListItemDto>> GetAsync(BookingAdminFilter filter, CancellationToken cancellationToken);

    Task<BookingAdminDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken);
}
