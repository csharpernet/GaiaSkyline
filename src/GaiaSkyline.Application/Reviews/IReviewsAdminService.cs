namespace GaiaSkyline.Application.Reviews;

public sealed record ReviewAdminDto(
    Guid Id,
    int Rating,
    string GuestFirstName,
    string? GuestLocation,
    string Body,
    string Source,
    DateOnly StayedOn,
    bool IsPublished);

public sealed record ReviewWriteModel(
    int Rating,
    string GuestFirstName,
    string? GuestLocation,
    string Body,
    string Source,
    DateOnly StayedOn);

public sealed record ReviewAdminResult(bool Ok, string? Error)
{
    public static ReviewAdminResult Success { get; } = new(true, null);

    public static ReviewAdminResult Fail(string error) => new(false, error);
}

/// <summary>
/// Owner review management (Stage 7 §10): list everything, add off-platform reviews, fix typos,
/// publish/unpublish. Every mutation bumps the content revision so the home page (and its
/// AggregateRating/Review JSON-LD, computed from published reviews) refreshes on the next request.
/// </summary>
public interface IReviewsAdminService
{
    Task<IReadOnlyList<ReviewAdminDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<ReviewAdminResult> CreateAsync(ReviewWriteModel review, bool publish, CancellationToken cancellationToken);

    Task<ReviewAdminResult> UpdateAsync(Guid id, ReviewWriteModel review, CancellationToken cancellationToken);

    Task<ReviewAdminResult> SetPublishedAsync(Guid id, bool published, CancellationToken cancellationToken);
}
