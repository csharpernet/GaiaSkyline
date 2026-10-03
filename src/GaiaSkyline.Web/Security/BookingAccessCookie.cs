using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace GaiaSkyline.Web.Security;

/// <summary>
/// A short-lived, data-protection-signed cookie granting a guest (no account) access to one booking
/// after they consume a magic link. Scoped to a single reference and expires in two hours.
/// </summary>
public sealed class BookingAccessCookie(IDataProtectionProvider dataProtectionProvider, TimeProvider clock)
{
    public const string CookieName = "gaia_booking";

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("GaiaSkyline.BookingAccess.v1");

    public void Grant(HttpResponse response, string reference)
    {
        var expires = clock.GetUtcNow().Add(Lifetime);
        var payload = _protector.Protect($"{reference.Trim().ToUpperInvariant()}|{expires.UtcTicks}");
        response.Cookies.Append(CookieName, payload, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = expires,
            IsEssential = true,
        });
    }

    public bool HasAccess(HttpRequest request, string reference)
    {
        if (!request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            var parts = _protector.Unprotect(value).Split('|');
            return parts.Length == 2
                && long.TryParse(parts[1], out var ticks)
                && clock.GetUtcNow().UtcTicks <= ticks
                && string.Equals(parts[0], reference.Trim().ToUpperInvariant(), StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
