namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a pricing rule. See ADR 0002.</summary>
public readonly record struct PricingRuleId(Guid Value)
{
    public static PricingRuleId New() => new(Guid.NewGuid());

    public static PricingRuleId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
