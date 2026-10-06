using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Partners;

/// <summary>
/// A single-use onboarding invite for an approved partner, valid for seven days (Stage 8 Part A). The emailed
/// link carries a DataProtection-wrapped copy of the row id (same pattern as the guest magic link); this row
/// is the revocable server-side state.
/// </summary>
public sealed class PartnerInvite : Entity<PartnerInviteId>
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    // Required by EF Core's materialization.
    private PartnerInvite()
    {
    }

    public PartnerInvite(PartnerInviteId id, PartnerId partnerId, DateTime createdAtUtc)
    {
        Id = id;
        PartnerId = partnerId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = createdAtUtc + Lifetime;
    }

    public PartnerId PartnerId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public bool IsUsable(DateTime nowUtc) => ConsumedAtUtc is null && nowUtc <= ExpiresAtUtc;

    public void Consume(DateTime nowUtc)
    {
        if (!IsUsable(nowUtc))
        {
            throw new InvalidOperationException("This invite has expired or was already used.");
        }

        ConsumedAtUtc = nowUtc;
    }
}
