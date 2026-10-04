namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for one transcoded hero-video rendition row. See ADR 0002.</summary>
public readonly record struct HeroVideoRenditionId(Guid Value)
{
    public static HeroVideoRenditionId New() => new(Guid.NewGuid());

    public static HeroVideoRenditionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
