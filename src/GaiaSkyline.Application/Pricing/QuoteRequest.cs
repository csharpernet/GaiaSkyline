namespace GaiaSkyline.Application.Pricing;

/// <summary>A request for a price quote. <see cref="PromoCode"/> is the raw code the guest entered.</summary>
public sealed record QuoteRequest(
    DateOnly CheckIn,
    DateOnly CheckOut,
    GuestParty Guests,
    string? PromoCode = null);
