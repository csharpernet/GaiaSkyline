namespace GaiaSkyline.Application.Pricing;

/// <summary>The party on a booking. Infants are excluded from the occupancy cap and never pay tax.</summary>
public sealed record GuestParty(int Adults, int Children, int Infants);
