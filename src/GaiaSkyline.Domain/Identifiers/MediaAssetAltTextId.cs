namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a per-language media alt-text row. See ADR 0002.</summary>
public readonly record struct MediaAssetAltTextId(Guid Value)
{
    public static MediaAssetAltTextId New() => new(Guid.NewGuid());

    public static MediaAssetAltTextId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
