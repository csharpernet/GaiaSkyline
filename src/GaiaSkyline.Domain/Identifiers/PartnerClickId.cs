namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a recorded partner referral click (Stage 8 Part A).</summary>
public readonly record struct PartnerClickId(Guid Value)
{
    public static PartnerClickId New() => new(Guid.NewGuid());

    public static PartnerClickId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
