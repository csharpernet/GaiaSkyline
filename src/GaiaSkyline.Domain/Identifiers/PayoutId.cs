namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a partner payout (Stage 8 Part A).</summary>
public readonly record struct PayoutId(Guid Value)
{
    public static PayoutId New() => new(Guid.NewGuid());

    public static PayoutId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
