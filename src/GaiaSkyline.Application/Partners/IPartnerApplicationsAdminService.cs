namespace GaiaSkyline.Application.Partners;

public sealed record PartnerApplicationDto(
    Guid Id,
    string Name,
    string Email,
    string? SocialLinks,
    int? AudienceSize,
    string? Niche,
    string? Message,
    string Status,
    DateTime SubmittedAtUtc,
    DateTime? DecidedAtUtc,
    string? DecisionNote);

public sealed record PartnerApplicationResult(bool Ok, string? Error)
{
    public static PartnerApplicationResult Success { get; } = new(true, null);

    public static PartnerApplicationResult Fail(string error) => new(false, error);
}

/// <summary>
/// Partner-application review for the admin (Stage 7 §11): list everything newest-first and record a
/// single approve/reject decision per application. The partner program itself ships in Stage 8.
/// </summary>
public interface IPartnerApplicationsAdminService
{
    Task<IReadOnlyList<PartnerApplicationDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<PartnerApplicationResult> ApproveAsync(Guid id, string? note, CancellationToken cancellationToken);

    Task<PartnerApplicationResult> RejectAsync(Guid id, string? note, CancellationToken cancellationToken);
}
