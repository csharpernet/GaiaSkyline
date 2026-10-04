using System.Text.RegularExpressions;
using GaiaSkyline.Application.Media;

namespace GaiaSkyline.Web.Middleware;

/// <summary>
/// Keeps old image URLs working after an SEO rename (Stage 7E-2d). It runs just after the static-file handler,
/// so it only sees requests the handler did not serve. For a missing <c>/media/{stem}-{w}.{ext}</c> whose stem
/// is a known previous filename, it 301-redirects to the asset's current filename; otherwise it passes through
/// (a genuine 404). Live files never reach here, so there is no per-request cost on the hot path.
/// </summary>
public sealed partial class MediaAliasRedirectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IMediaAliasResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
            && request.Path.StartsWithSegments("/media", out var remainder)
            && remainder.HasValue)
        {
            var fileName = remainder.Value!.TrimStart('/');
            var match = RenditionName().Match(fileName);
            if (match.Success)
            {
                var oldStem = match.Groups["stem"].Value;
                var currentStem = await resolver.ResolveCurrentStemAsync(oldStem, context.RequestAborted);
                if (currentStem is not null && !string.Equals(currentStem, oldStem, StringComparison.Ordinal))
                {
                    var target = $"/media/{currentStem}-{match.Groups["rest"].Value}{request.QueryString}";
                    context.Response.Redirect(target, permanent: true);
                    return;
                }
            }
        }

        await next(context);
    }

    // {stem}-{width}.{ext}; the stem may itself contain hyphens, so anchor on the final -{digits}.{ext}.
    [GeneratedRegex(@"^(?<stem>.+)-(?<rest>\d+\.(?:jpg|jpeg|webp|avif))$", RegexOptions.IgnoreCase)]
    private static partial Regex RenditionName();
}
