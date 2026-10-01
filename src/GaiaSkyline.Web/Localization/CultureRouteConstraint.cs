using System.Globalization;

namespace GaiaSkyline.Web.Localization;

/// <summary>
/// Route constraint ("culture") that matches only the five enabled language slugs. An unknown
/// language segment therefore fails to match the route and yields a 404 (we never guess).
/// </summary>
public sealed class CultureRouteConstraint : IRouteConstraint
{
    public bool Match(
        HttpContext? httpContext,
        IRouter? route,
        string routeKey,
        RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (!values.TryGetValue(routeKey, out var value) || value is null)
        {
            return false;
        }

        var slug = Convert.ToString(value, CultureInfo.InvariantCulture);
        return SupportedCultures.IsValidSlug(slug);
    }
}
