using GaiaSkyline.Web.Content;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>Owner content preview: grant/clear the signed preview cookie, then view the public site with
/// drafts rendered (and the output cache bypassed).</summary>
[Route("admin/preview")]
public sealed class PreviewController(ContentPreview preview) : AdminControllerBase
{
    [HttpGet("enter")]
    public IActionResult Enter(string? returnUrl)
    {
        preview.Grant(Response);
        return LocalRedirect(Safe(returnUrl));
    }

    [HttpGet("exit")]
    public IActionResult Exit(string? returnUrl)
    {
        preview.Revoke(Response);
        return LocalRedirect(Safe(returnUrl));
    }

    private string Safe(string? url) => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : "/en";
}
