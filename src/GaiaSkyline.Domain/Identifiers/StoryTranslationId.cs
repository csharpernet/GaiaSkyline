namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a story translation. See ADR 0002.</summary>
public readonly record struct StoryTranslationId(Guid Value)
{
    public static StoryTranslationId New() => new(Guid.NewGuid());

    public static StoryTranslationId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
