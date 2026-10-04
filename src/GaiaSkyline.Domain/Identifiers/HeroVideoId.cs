namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a hero background-video version. See ADR 0002.</summary>
public readonly record struct HeroVideoId(Guid Value)
{
    public static HeroVideoId New() => new(Guid.NewGuid());

    public static HeroVideoId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
