namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a booking's partner attribution (Stage 8 Part A).</summary>
public readonly record struct PartnerAttributionId(Guid Value)
{
    public static PartnerAttributionId New() => new(Guid.NewGuid());

    public static PartnerAttributionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
