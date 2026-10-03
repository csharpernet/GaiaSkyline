using Microsoft.AspNetCore.DataProtection;

namespace GaiaSkyline.Infrastructure.Availability;

/// <summary>
/// Encrypts/decrypts external-calendar iCal URLs at rest (Data Protection). The URL is a credential:
/// store only the ciphertext, never log it, and show only a masked tail in any API output.
/// </summary>
public sealed class ExternalCalendarUrlProtector(IDataProtectionProvider dataProtectionProvider)
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("GaiaSkyline.ExternalCalendarUrl.v1");

    public string Protect(string url) => _protector.Protect(url);

    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);

    /// <summary>Shows only the last four characters of a URL (for admin display).</summary>
    public static string Mask(string url) =>
        string.IsNullOrEmpty(url) || url.Length <= 4 ? "••••" : "••••" + url[^4..];
}
