using Microsoft.AspNetCore.Localization;

namespace GaiaSkyline.Web.Localization;

/// <summary>
/// Resolves the request culture from the <c>{lang}</c> route segment (the URL is authoritative for
/// a page's language). Returns null when there is no valid lang segment (e.g. the API or root),
/// letting the next provider decide.
/// </summary>
public sealed class RouteCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var slug = httpContext.Request.RouteValues.TryGetValue("lang", out var value)
            ? value?.ToString()
            : null;

        var option = SupportedCultures.TryGetBySlug(slug);
        return option is null
            ? NullProviderCultureResult
            : Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(option.Culture, option.Culture));
    }
}
