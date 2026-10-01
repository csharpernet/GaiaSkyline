namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a guest review. See ADR 0002.</summary>
public readonly record struct ReviewId(Guid Value)
{
    public static ReviewId New() => new(Guid.NewGuid());

    public static ReviewId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
