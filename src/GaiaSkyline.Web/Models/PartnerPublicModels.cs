using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Partners;

namespace GaiaSkyline.Web.Models;

/// <summary>The partner-program landing page (Stage 8 Part A); copy comes from the partners.* blocks.</summary>
public sealed record PartnersLandingViewModel(ContentPayload Content);

/// <summary>The application form posted from /partners/apply. <see cref="Website"/> is the honeypot.</summary>
public sealed class PartnerApplyForm
{
    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? SocialLinks { get; set; }

    public int? AudienceSize { get; set; }

    public string? Niche { get; set; }

    public string? Message { get; set; }

    public bool Consent { get; set; }

    /// <summary>Honeypot: hidden from humans; a filled value means a bot and the submission is dropped.</summary>
    public string? Website { get; set; }
}

public sealed record PartnerApplyViewModel(
    ContentPayload Content,
    PartnerApplyForm Form,
    bool Submitted,
    string? Error);

/// <summary>The onboarding form behind the single-use invite link (Stage 8 Part A).</summary>
public sealed class PartnerJoinForm
{
    public string Token { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool AcceptTerms { get; set; }

    public string Iban { get; set; } = string.Empty;

    public string AccountHolder { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
}

public sealed record PartnerJoinViewModel(
    PartnerInvitePreview? Invite,
    string Token,
    string TermsHtml,
    string? Error,
    bool Completed);

/// <summary>The partner dashboard page (Stage 8 Part A).</summary>
public sealed record PartnerPortalViewModel(PartnerDashboardDto Partner, string? Error, bool Saved);

/// <summary>Payout details edited from the partner profile (code/percentages stay read-only).</summary>
public sealed class PartnerPayoutDetailsForm
{
    public string Iban { get; set; } = string.Empty;

    public string AccountHolder { get; set; } = string.Empty;

    public string TaxId { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;
}
