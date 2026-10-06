namespace GaiaSkyline.Application.Partners;

/// <summary>Outcome of inviting an approved applicant into the program.</summary>
public sealed record PartnerInviteResult(bool Ok, Guid? PartnerId, string? Error)
{
    public static PartnerInviteResult Success(Guid partnerId) => new(true, partnerId, null);

    public static PartnerInviteResult Fail(string error) => new(false, null, error);
}

/// <summary>What the onboarding page needs to render for a valid invite token.</summary>
public sealed record PartnerInvitePreview(Guid PartnerId, string Name, string Email, string PromoCode);

/// <summary>Everything the partner submits to complete onboarding (Stage 8 Part A).</summary>
public sealed record PartnerOnboardingSubmission(
    string Token,
    string Password,
    bool TermsAccepted,
    string Iban,
    string AccountHolder,
    string TaxId,
    string Country);

public sealed record PartnerOnboardingResult(bool Ok, string? Error)
{
    public static PartnerOnboardingResult Success() => new(true, null);

    public static PartnerOnboardingResult Fail(string error) => new(false, error);
}

/// <summary>
/// Partner onboarding (Stage 8 Part A): approving an application creates the partner with the program
/// defaults and an auto-generated promo code, and emails a single-use invite link valid for seven days.
/// Completing the link sets the password (HIBP-checked), records the accepted terms version, stores payout
/// details and activates the partner's login.
/// </summary>
public interface IPartnerOnboardingService
{
    /// <summary>Creates the partner for an approved application (idempotent) and sends the invite email.</summary>
    Task<PartnerInviteResult> InviteAsync(Guid applicationId, CancellationToken cancellationToken);

    /// <summary>Re-issues and re-sends an invite for a partner still in the Invited state.</summary>
    Task<PartnerInviteResult> ResendInviteAsync(Guid partnerId, CancellationToken cancellationToken);

    /// <summary>Resolves an invite token to its partner without consuming it; null when invalid/expired/used.</summary>
    Task<PartnerInvitePreview?> PreviewAsync(string token, CancellationToken cancellationToken);

    /// <summary>Consumes the invite: creates the Partner-role user and activates the partner.</summary>
    Task<PartnerOnboardingResult> CompleteAsync(PartnerOnboardingSubmission submission, CancellationToken cancellationToken);
}
