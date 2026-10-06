namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a partner onboarding invite (Stage 8 Part A).</summary>
public readonly record struct PartnerInviteId(Guid Value)
{
    public static PartnerInviteId New() => new(Guid.NewGuid());

    public static PartnerInviteId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
