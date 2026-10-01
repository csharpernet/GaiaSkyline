using System.Globalization;
using GaiaSkyline.Application.Content;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>
/// Public, read-only content API. The language is resolved by the request-localization middleware
/// (?lang → cookie → Accept-Language → "en") and surfaced here as the current UI culture.
/// Writes arrive with the admin area in a later stage.
/// </summary>
[ApiController]
[Route("api/content")]
public sealed class ContentController(IContentService contentService) : ControllerBase
{
    [HttpGet("{section}")]
    [ProducesResponseType<ContentPayload>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ContentPayload>> GetSection(
        string section,
        CancellationToken cancellationToken)
    {
        var language = CultureInfo.CurrentUICulture.Name;
        var payload = await contentService.GetSectionAsync(section, language, cancellationToken);
        return Ok(payload);
    }
}
