namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a promo code. See ADR 0002.</summary>
public readonly record struct PromoCodeId(Guid Value)
{
    public static PromoCodeId New() => new(Guid.NewGuid());

    public static PromoCodeId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
