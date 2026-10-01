namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a content translation. See ADR 0002.</summary>
public readonly record struct ContentTranslationId(Guid Value)
{
    public static ContentTranslationId New() => new(Guid.NewGuid());

    public static ContentTranslationId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
