namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a partner application. See ADR 0002.</summary>
public readonly record struct PartnerApplicationId(Guid Value)
{
    public static PartnerApplicationId New() => new(Guid.NewGuid());

    public static PartnerApplicationId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
