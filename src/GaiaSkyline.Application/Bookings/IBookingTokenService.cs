namespace GaiaSkyline.Application.Bookings;

/// <summary>
/// Issues and validates the unguessable token that gates a confirmation page, so links can't be
/// enumerated from the (human-readable) reference code alone.
/// </summary>
public interface IBookingTokenService
{
    string CreateConfirmationToken(string referenceCode);

    bool IsValidConfirmationToken(string referenceCode, string token);
}
