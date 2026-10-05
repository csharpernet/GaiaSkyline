using GaiaSkyline.Application.Seo;

namespace GaiaSkyline.Web.Middleware;

/// <summary>
/// Applies owner-configured redirect rules before routing: a request whose path matches a rule is sent to the
/// rule's target with a 301 (permanent) or 302. Backed by an in-memory index (see <see cref="IRedirectResolver"/>),
/// so unmatched requests cost only a dictionary lookup. Stage 7 §5.
/// </summary>
public sealed class RedirectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRedirectResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = context.Request;
        if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) && request.Path.HasValue)
        {
            var target = await resolver.ResolveAsync(request.Path.Value!, context.RequestAborted);
            if (target is not null)
            {
                context.Response.Redirect(target.ToPath + request.QueryString, target.Permanent);
                return;
            }
        }

        await next(context);
    }
}
