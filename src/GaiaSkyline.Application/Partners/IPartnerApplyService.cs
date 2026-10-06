namespace GaiaSkyline.Application.Partners;

/// <summary>What the public application form submits (Stage 8 Part A).</summary>
public sealed record PartnerApplySubmission(
    string Name,
    string Email,
    string? SocialLinks,
    int? AudienceSize,
    string? Niche,
    string? Message);

public sealed record PartnerApplyResult(bool Ok, string? Error)
{
    public static PartnerApplyResult Success() => new(true, null);

    public static PartnerApplyResult Fail(string error) => new(false, error);
}

/// <summary>Accepts public partner applications and notifies the owner (Stage 8 Part A).</summary>
public interface IPartnerApplyService
{
    Task<PartnerApplyResult> SubmitAsync(PartnerApplySubmission submission, CancellationToken cancellationToken);
}
