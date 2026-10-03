using GaiaSkyline.Application.Bookings;
using Microsoft.AspNetCore.DataProtection;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>
/// Data-protection-backed confirmation tokens. The token is the protected reference code, so it is
/// unforgeable and tied to that booking; validation unprotects and compares.
/// </summary>
internal sealed class BookingTokenService(IDataProtectionProvider dataProtectionProvider) : IBookingTokenService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("GaiaSkyline.BookingConfirmation.v1");

    public string CreateConfirmationToken(string referenceCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceCode);
        return _protector.Protect(referenceCode.Trim().ToUpperInvariant());
    }

    public bool IsValidConfirmationToken(string referenceCode, string token)
    {
        if (string.IsNullOrWhiteSpace(referenceCode) || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var unprotected = _protector.Unprotect(token);
            return string.Equals(unprotected, referenceCode.Trim().ToUpperInvariant(), StringComparison.Ordinal);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }
}
