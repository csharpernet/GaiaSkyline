using GaiaSkyline.Application.Content;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Api;

/// <summary>Public, read-only reviews API (most recent first).</summary>
[ApiController]
[Route("api/reviews")]
public sealed class ReviewsController(IContentService contentService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ReviewDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReviewDto>>> GetPublished(CancellationToken cancellationToken)
    {
        var reviews = await contentService.GetPublishedReviewsAsync(cancellationToken);
        return Ok(reviews);
    }
}
