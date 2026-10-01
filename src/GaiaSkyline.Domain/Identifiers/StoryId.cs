namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a story (blog entry). See ADR 0002.</summary>
public readonly record struct StoryId(Guid Value)
{
    public static StoryId New() => new(Guid.NewGuid());

    public static StoryId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
