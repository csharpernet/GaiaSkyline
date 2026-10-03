namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for an owner-entered calendar block. See ADR 0002.</summary>
public readonly record struct OwnerBlockId(Guid Value)
{
    public static OwnerBlockId New() => new(Guid.NewGuid());

    public static OwnerBlockId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
