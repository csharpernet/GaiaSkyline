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

        // The Stripe payment pages (checkout, confirmation) need narrow extra allowances for js.stripe.com
        // and the Payment Request API (Apple Pay / Google Pay). See ADR 0011.
        var needsStripe = NeedsStripe(context.Request.Path);

        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["X-Frame-Options"] = "DENY";
        headers["Permissions-Policy"] = BuildPermissionsPolicy(needsStripe);
        headers["Content-Security-Policy"] = BuildContentSecurityPolicy(nonce, needsStripe);

        await next(context);
    }

    private static bool NeedsStripe(PathString path) =>
        path.HasValue &&
        (path.Value!.Contains("/book/checkout", StringComparison.OrdinalIgnoreCase)
         || path.Value!.Contains("/book/confirmation", StringComparison.OrdinalIgnoreCase));

    private static string BuildPermissionsPolicy(bool needsStripe)
    {
        // Apple Pay / Google Pay use the Payment Request API, which the payment directive gates.
        var payment = needsStripe ? "payment=(self \"https://js.stripe.com\")" : "payment=()";
        return
            "accelerometer=(), autoplay=(), camera=(), display-capture=(), encrypted-media=(), " +
            "fullscreen=(self), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), " +
            $"midi=(), {payment}, usb=()";
    }

    private static string GenerateNonce()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    // The lazy MapLibre location map now streams its raster tiles through our own origin (the
    // /map/tiles/{z}/{x}/{y}.png proxy, Stage 7 §5 / ADR 0018), so 'self' covers them and no external
    // tile host is whitelisted. It still runs its renderer in a blob: worker (worker-src blob:).
    private const string StripeJs = "https://js.stripe.com";
    private const string StripeApi = "https://api.stripe.com";
    private const string StripeHooks = "https://hooks.stripe.com";

    private static string BuildContentSecurityPolicy(string nonce, bool needsStripe)
    {
        var scriptSrc = needsStripe
            ? $"script-src 'self' 'nonce-{nonce}' {StripeJs}"
            : $"script-src 'self' 'nonce-{nonce}'";
        var connectSrc = needsStripe
            ? $"connect-src 'self' {StripeApi}"
            : "connect-src 'self'";
        var frameSrc = needsStripe
            ? $"frame-src {StripeJs} {StripeHooks}"
            : "frame-src 'none'";

        return string.Join("; ",
            "default-src 'self'",
            "base-uri 'self'",
            "object-src 'none'",
            "frame-ancestors 'none'",
            "img-src 'self' data: blob:",
            // Hero videos are served from our origin; the admin previews the chosen file via a blob: URL.
            "media-src 'self' blob:",
            "font-src 'self' https://fonts.gstatic.com",
            $"style-src 'self' https://fonts.googleapis.com 'nonce-{nonce}'",
            scriptSrc,
            "worker-src 'self' blob:",
            connectSrc,
            frameSrc,
            "form-action 'self'",
            "upgrade-insecure-requests");
    }
}
