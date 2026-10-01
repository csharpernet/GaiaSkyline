namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a media collection. See ADR 0002.</summary>
public readonly record struct MediaCollectionId(Guid Value)
{
    public static MediaCollectionId New() => new(Guid.NewGuid());

    public static MediaCollectionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
