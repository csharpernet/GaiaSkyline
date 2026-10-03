using GaiaSkyline.Application.Pricing;

namespace GaiaSkyline.Application.Bookings;

/// <summary>
/// Everything needed to create a booking. The server re-quotes from these inputs and never trusts a
/// client-supplied price.
/// </summary>
public sealed record CreateBookingCommand(
    DateOnly CheckIn,
    DateOnly CheckOut,
    GuestParty Guests,
    string GuestName,
    string GuestEmail,
    string GuestPhone,
    string GuestCountry,
    string GuestLanguage,
    string? PromoCode = null,
    TimeOnly? ArrivalEstimateLocal = null,
    string? SpecialRequests = null,
    bool AccountCreationRequested = false);
