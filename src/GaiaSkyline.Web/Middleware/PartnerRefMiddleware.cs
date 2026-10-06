using GaiaSkyline.Application.Partners;

namespace GaiaSkyline.Web.Middleware;

/// <summary>
/// Referral links (Stage 8 Part A, ADR 0019): a public GET arriving with <c>?ref=CODE</c> records a
/// <c>PartnerClick</c>, sets the 30-day first-party <c>gs_ref</c> cookie (plus a random anonymous visitor id
/// so unique visitors can be counted without PII) and 301-redirects to the same URL without <c>ref</c> —
/// the parameter never reaches routing, caches or the search index. Unknown or suspended codes just strip.
/// </summary>
public sealed class PartnerRefMiddleware(RequestDelegate next)
{
    public const string RefCookieName = "gs_ref";
    public const string AnonymousCookieName = "gs_anon";
    private const string RefParameter = "ref";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(30);

    public async Task InvokeAsync(HttpContext context, IPartnerAttributionService attribution)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method)
            || !request.Query.TryGetValue(RefParameter, out var refValues)
            || request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var code = refValues.ToString().Trim();
        var landingPath = request.Path.Value ?? "/";
        if (code.Length is > 0 and <= 50)
        {
            var anonymousId = EnsureAnonymousId(context);
            if (await attribution.RecordClickAsync(code, landingPath, anonymousId, context.RequestAborted))
            {
                context.Response.Cookies.Append(RefCookieName, code.ToUpperInvariant(), new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    MaxAge = CookieLifetime,
                    IsEssential = true,
                });
            }
        }

        // 301 to the same URL without ref (other query parameters survive).
        var remaining = request.Query
            .Where(kv => !string.Equals(kv.Key, RefParameter, StringComparison.OrdinalIgnoreCase))
            .SelectMany(kv => kv.Value, (kv, v) => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(v ?? string.Empty)}")
            .ToList();
        var target = landingPath + (remaining.Count > 0 ? "?" + string.Join("&", remaining) : string.Empty);
        context.Response.Redirect(target, permanent: true);
    }

    private static Guid EnsureAnonymousId(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(AnonymousCookieName, out var existing)
            && Guid.TryParse(existing, out var id))
        {
            return id;
        }

        var fresh = Guid.NewGuid();
        context.Response.Cookies.Append(AnonymousCookieName, fresh.ToString("N"), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = CookieLifetime,
            IsEssential = false,
        });
        return fresh;
    }
}
