using GaiaSkyline.Web.Localization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>Root redirect ("/") and the language switcher endpoint.</summary>
public sealed class RootController : Controller
{
    /// <summary>302 (not 301 — preference can change) to the visitor's best language.</summary>
    [HttpGet("/")]
    public IActionResult Index()
    {
        var slug = LanguageNegotiation.ResolveSlug(HttpContext);
        return Redirect($"/{slug}");
    }

    /// <summary>Records the chosen language in the culture cookie and returns to the equivalent URL.</summary>
    [HttpGet("culture/set")]
    public IActionResult SetLanguage(string culture, string? returnUrl)
    {
        var option = SupportedCultures.TryGetByCulture(culture);
        var slug = option?.Slug ?? SupportedCultures.DefaultSlug;

        if (option is not null)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(option.Culture)),
                new CookieOptions
                {
                    Path = "/",
                    SameSite = SameSiteMode.Lax,
                    IsEssential = true,
                    MaxAge = TimeSpan.FromDays(365),
                });
        }

        var target = returnUrl is not null && Url.IsLocalUrl(returnUrl) ? returnUrl : $"/{slug}";
        return Redirect(target);
    }
}
