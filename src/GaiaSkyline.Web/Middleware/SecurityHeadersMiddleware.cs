using System.Security.Cryptography;

namespace GaiaSkyline.Web.Middleware;

/// <summary>
/// Emits a strict set of security response headers, including a Content-Security-Policy with a
/// fresh per-request nonce. The nonce is stashed in <see cref="HttpContext.Items"/> so views can
/// stamp any inline &lt;script&gt;/&lt;style&gt; with <c>nonce="@Context.Items["csp-nonce"]"</c>.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string NonceItemKey = "csp-nonce";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var nonce = GenerateNonce();
        context.Items[NonceItemKey] = nonce;

        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-Frame-Options"] = "DENY";
        headers["Permissions-Policy"] =
            "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), " +
            "fullscreen=(self), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), " +
            "midi=(), payment=(), usb=()";
        headers["Content-Security-Policy"] = BuildContentSecurityPolicy(nonce);

        await next(context);
    }

    private static string GenerateNonce()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string BuildContentSecurityPolicy(string nonce) =>
        string.Join("; ",
            "default-src 'self'",
            "base-uri 'self'",
            "object-src 'none'",
            "frame-ancestors 'none'",
            "img-src 'self' data:",
            "font-src 'self' https://fonts.gstatic.com",
            $"style-src 'self' https://fonts.googleapis.com 'nonce-{nonce}'",
            $"script-src 'self' 'nonce-{nonce}'",
            "connect-src 'self'",
            "form-action 'self'",
            "upgrade-insecure-requests");
}
