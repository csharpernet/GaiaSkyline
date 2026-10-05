using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Models;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

public sealed class LegalController(IContentService content) : PublicController
{
    private static readonly Dictionary<string, string> Pages = new(StringComparer.Ordinal)
    {
        ["terms"] = "Terms & Conditions",
        ["privacy"] = "Privacy Policy",
        ["cancellation-policy"] = "Cancellation Policy",
        ["al-registration"] = "AL Registration",
    };

    [HttpGet("{lang:culture}/legal/{page}")]
    public async Task<IActionResult> Index(string page, CancellationToken cancellationToken)
    {
        if (!Pages.TryGetValue(page, out var heading))
        {
            return NotFound();
        }

        var footer = await content.GetSectionAsync("footer", CurrentCulture, cancellationToken);
        var registration = footer.TextOr("footer.registration_value", "175890/AL");

        await SetMetaAsync(Meta(
            relativePath: $"legal/{page}",
            title: $"{heading} — {BrandName}",
            description: $"{heading} for the Gaia Skyline apartment in Vila Nova de Gaia (AL registration {registration}).",
            breadcrumbs: [new Breadcrumb("Home", string.Empty), new Breadcrumb(heading, null)]), cancellationToken);

        return View(new LegalViewModel(page, heading, registration));
    }
}
