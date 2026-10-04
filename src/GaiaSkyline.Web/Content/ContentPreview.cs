using System.Globalization;
using System.Security.Cryptography;
using GaiaSkyline.Application.Content;
using Microsoft.AspNetCore.DataProtection;

namespace GaiaSkyline.Web.Content;

/// <summary>
/// The owner content-preview cookie: a DataProtection-signed, 1-hour token. Only the owner can mint it
/// (the controller that grants it is behind the Owner policy), so guests can never see drafts. When a
/// valid cookie is present, <see cref="IContentPreviewState.IsPreview"/> is true — the content read then
/// shows draft values and unpublished blocks, and the output cache is bypassed.
/// </summary>
public sealed class ContentPreview : IContentPreviewState
{
    public const string CookieName = "gaia_preview";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    private readonly IDataProtector _protector;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _clock;

    public ContentPreview(
        IDataProtectionProvider dataProtectionProvider,
        IHttpContextAccessor httpContextAccessor,
        TimeProvider clock)
    {
        _protector = dataProtectionProvider.CreateProtector("GaiaSkyline.ContentPreview.v1");
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
    }

    public bool IsPreview
    {
        get
        {
            var token = _httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            try
            {
                var expiresUnix = long.Parse(_protector.Unprotect(token), CultureInfo.InvariantCulture);
                return DateTimeOffset.FromUnixTimeSeconds(expiresUnix) > _clock.GetUtcNow();
            }
            catch (Exception ex) when (ex is CryptographicException or FormatException)
            {
                return false;
            }
        }
    }

    public void Grant(HttpResponse response)
    {
        var expires = _clock.GetUtcNow().Add(Lifetime);
        var token = _protector.Protect(expires.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = expires,
        });
    }

    public void Revoke(HttpResponse response) => response.Cookies.Delete(CookieName);
}
