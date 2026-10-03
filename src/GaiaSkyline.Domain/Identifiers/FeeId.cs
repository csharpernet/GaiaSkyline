namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a fee (cleaning, tourist tax). See ADR 0002.</summary>
public readonly record struct FeeId(Guid Value)
{
    public static FeeId New() => new(Guid.NewGuid());

    public static FeeId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
