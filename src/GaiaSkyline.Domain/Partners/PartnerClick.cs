using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Partners;

/// <summary>
/// One referral click: a public request that arrived with <c>?ref=CODE</c> (Stage 8 Part A). Stores the
/// landing path and an anonymous visitor id only — never the IP address.
/// </summary>
public sealed class PartnerClick : Entity<PartnerClickId>
{
    // Required by EF Core's materialization.
    private PartnerClick()
    {
    }

    public PartnerClick(PartnerClickId id, PartnerId partnerId, string landingPath, DateTime utcAt, Guid anonymousId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(landingPath);

        Id = id;
        PartnerId = partnerId;
        LandingPath = landingPath.Length > 400 ? landingPath[..400] : landingPath;
        UtcAt = utcAt;
        AnonymousId = anonymousId;
    }

    public PartnerId PartnerId { get; private set; }

    /// <summary>The site-relative path the visitor landed on, without the <c>ref</c> parameter.</summary>
    public string LandingPath { get; private set; } = null!;

    public DateTime UtcAt { get; private set; }

    /// <summary>A random first-party visitor id (cookie), so unique visitors can be estimated without PII.</summary>
    public Guid AnonymousId { get; private set; }
}
