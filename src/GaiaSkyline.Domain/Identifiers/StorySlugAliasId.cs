namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a story's previous-slug alias row. See ADR 0002.</summary>
public readonly record struct StorySlugAliasId(Guid Value)
{
    public static StorySlugAliasId New() => new(Guid.NewGuid());

    public static StorySlugAliasId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
