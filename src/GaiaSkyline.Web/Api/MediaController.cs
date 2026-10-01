using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Identifiers;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>Public, read-only media metadata API.</summary>
[ApiController]
[Route("api/media")]
public sealed class MediaController(IContentService contentService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ProducesResponseType<MediaAssetDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MediaAssetDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var asset = await contentService.GetMediaAssetAsync(MediaAssetId.From(id), cancellationToken);
        return asset is null ? NotFound() : Ok(asset);
    }
}
