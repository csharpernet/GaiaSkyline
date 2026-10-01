using System.Globalization;
using GaiaSkyline.Application.Content;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.ViewComponents;

/// <summary>Renders the site footer, reading footer content for the current culture.</summary>
public sealed class SiteFooterViewComponent(IContentService content) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var footer = await content.GetSectionAsync("footer", CultureInfo.CurrentUICulture.Name, HttpContext.RequestAborted);
        return View(footer);
    }
}
