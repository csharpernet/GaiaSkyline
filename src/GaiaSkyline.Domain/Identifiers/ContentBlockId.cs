namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a content block. See ADR 0002.</summary>
public readonly record struct ContentBlockId(Guid Value)
{
    public static ContentBlockId New() => new(Guid.NewGuid());

    public static ContentBlockId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
