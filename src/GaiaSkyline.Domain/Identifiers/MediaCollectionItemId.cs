namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a media collection item. See ADR 0002.</summary>
public readonly record struct MediaCollectionItemId(Guid Value)
{
    public static MediaCollectionItemId New() => new(Guid.NewGuid());

    public static MediaCollectionItemId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
