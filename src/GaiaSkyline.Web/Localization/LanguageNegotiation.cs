using Microsoft.AspNetCore.Localization;

namespace GaiaSkyline.Web.Localization;

/// <summary>Picks the best language slug for a visitor arriving at the root "/".</summary>
public static class LanguageNegotiation
{
    public static string ResolveSlug(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. An explicit cookie preference (set by the language switcher) wins.
        var cookie = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];
        if (cookie is not null)
        {
            var parsed = CookieRequestCultureProvider.ParseCookieValue(cookie);
            var culture = parsed?.UICultures.Count > 0 ? parsed.UICultures[0].Value : null;
            var option = SupportedCultures.TryGetByCulture(culture);
            if (option is not null)
            {
                return option.Slug;
            }
        }

        // 2. Accept-Language, highest quality first.
        foreach (var language in context.Request.GetTypedHeaders().AcceptLanguage
            .OrderByDescending(x => x.Quality ?? 1d))
        {
            var match = Match(language.Value.Value);
            if (match is not null)
            {
                return match.Slug;
            }
        }

        // 3. Default.
        return SupportedCultures.DefaultSlug;
    }

    private static CultureOption? Match(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "*")
        {
            return null;
        }

        var exact = SupportedCultures.TryGetByCulture(value);
        if (exact is not null)
        {
            return exact;
        }

        // Language-prefix match: "pt" -> pt-PT, "en-US" -> en.
        var prefix = value.Split('-')[0];
        return SupportedCultures.All.FirstOrDefault(
            c => c.Culture.Split('-')[0].Equals(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
