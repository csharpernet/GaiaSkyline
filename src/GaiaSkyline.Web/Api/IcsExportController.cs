using System.Security.Cryptography;
using System.Text;
using GaiaSkyline.Application.Availability;
using GaiaSkyline.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>
/// Public calendar export at a token-gated, non-localized URL (robots-excluded). External platforms
/// subscribe to this; the token is a credential, compared in constant time.
/// </summary>
[ApiController]
[Route("calendar")]
public sealed class IcsExportController(IIcsExportService export, IcsExportOptions options) : ControllerBase
{
    [HttpGet("{token}/gaia-skyline.ics")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(string token, CancellationToken cancellationToken)
    {
        if (!TokensMatch(token, options.Token))
        {
            return NotFound();
        }

        var result = await export.GetAsync(cancellationToken);

        if (string.Equals(Request.Headers.IfNoneMatch.ToString(), result.ETag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = result.ETag;
        Response.Headers.CacheControl = "public, max-age=600";
        return File(result.Content, "text/calendar; charset=utf-8");
    }

    private static bool TokensMatch(string provided, string expected)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(expected);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
