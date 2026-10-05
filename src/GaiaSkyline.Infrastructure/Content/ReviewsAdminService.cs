using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Reviews;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>Review CRUD for the admin (Stage 7 §10). Every mutation bumps the content revision —
/// the home output cache (and the JSON-LD computed from published reviews) refreshes on the next miss.</summary>
internal sealed class ReviewsAdminService(AppDbContext dbContext, IContentRevision revision) : IReviewsAdminService
{
    public async Task<IReadOnlyList<ReviewAdminDto>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Reviews.AsNoTracking()
            .OrderByDescending(r => r.StayedOn)
            .Select(r => new ReviewAdminDto(
                r.Id.Value, r.Rating, r.GuestFirstName, r.GuestLocation, r.Body, r.Source, r.StayedOn, r.IsPublished))
            .ToListAsync(cancellationToken);

    public async Task<ReviewAdminResult> CreateAsync(ReviewWriteModel review, bool publish, CancellationToken cancellationToken)
    {
        try
        {
            dbContext.Reviews.Add(new Review(
                ReviewId.New(), review.Rating, review.GuestFirstName, review.GuestLocation,
                review.Body, review.Source, review.StayedOn, publish));
        }
        catch (ArgumentException ex)
        {
            return ReviewAdminResult.Fail(ex.Message);
        }

        await SaveAndBumpAsync(cancellationToken);
        return ReviewAdminResult.Success;
    }

    public async Task<ReviewAdminResult> UpdateAsync(Guid id, ReviewWriteModel review, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Reviews.FirstOrDefaultAsync(r => r.Id == ReviewId.From(id), cancellationToken);
        if (existing is null)
        {
            return ReviewAdminResult.Fail("That review no longer exists.");
        }

        try
        {
            existing.Update(review.Rating, review.GuestFirstName, review.GuestLocation, review.Body, review.Source, review.StayedOn);
        }
        catch (ArgumentException ex)
        {
            return ReviewAdminResult.Fail(ex.Message);
        }

        await SaveAndBumpAsync(cancellationToken);
        return ReviewAdminResult.Success;
    }

    public async Task<ReviewAdminResult> SetPublishedAsync(Guid id, bool published, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Reviews.FirstOrDefaultAsync(r => r.Id == ReviewId.From(id), cancellationToken);
        if (existing is null)
        {
            return ReviewAdminResult.Fail("That review no longer exists.");
        }

        existing.SetPublished(published);
        await SaveAndBumpAsync(cancellationToken);
        return ReviewAdminResult.Success;
    }

    private async Task SaveAndBumpAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
    }
}
