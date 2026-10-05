using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Reviews;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// The owner reviews manager at /admin/reviews (Stage 7 §10): list, publish/unpublish, typo edits and
/// adding off-platform reviews. Mutations bump the content revision, so the home page and its
/// AggregateRating/Review JSON-LD (computed from published reviews) refresh on the next request.
/// </summary>
[Route("admin/reviews")]
public sealed class ReviewsAdminController(IReviewsAdminService reviews, IAuditLog audit) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Reviews";
        return View(await reviews.GetAllAsync(cancellationToken));
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int rating, string guestFirstName, string? guestLocation, string body, string source,
        DateOnly? stayedOn, bool publish, CancellationToken cancellationToken)
    {
        if (stayedOn is not { } stayed)
        {
            Toast("Pick the stay date.", "error");
            return LocalRedirect("/admin/reviews");
        }

        var result = await reviews.CreateAsync(
            new ReviewWriteModel(rating, guestFirstName, guestLocation, body, source, stayed), publish, cancellationToken);
        return await FinishAsync(result, "review.create", null, publish ? "Review added and published." : "Review added as a draft.", cancellationToken);
    }

    [HttpPost("{id:guid}/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        Guid id, int rating, string guestFirstName, string? guestLocation, string body, string source,
        DateOnly? stayedOn, CancellationToken cancellationToken)
    {
        if (stayedOn is not { } stayed)
        {
            Toast("Pick the stay date.", "error");
            return LocalRedirect("/admin/reviews");
        }

        var result = await reviews.UpdateAsync(
            id, new ReviewWriteModel(rating, guestFirstName, guestLocation, body, source, stayed), cancellationToken);
        return await FinishAsync(result, "review.update", id, "Review updated.", cancellationToken);
    }

    [HttpPost("{id:guid}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id, bool published, CancellationToken cancellationToken)
    {
        var result = await reviews.SetPublishedAsync(id, published, cancellationToken);
        return await FinishAsync(result, published ? "review.publish" : "review.unpublish", id,
            published ? "Review published — the home page refreshes on its next request." : "Review unpublished.", cancellationToken);
    }

    private async Task<IActionResult> FinishAsync(
        ReviewAdminResult result, string auditEvent, Guid? id, string success, CancellationToken cancellationToken)
    {
        if (!result.Ok)
        {
            Toast(result.Error ?? "The review could not be saved.", "error");
            return LocalRedirect("/admin/reviews");
        }

        await audit.WriteAsync(auditEvent, ActorId, Ip, "Review", id?.ToString(), null, cancellationToken);
        Toast(success);
        return LocalRedirect("/admin/reviews");
    }
}
