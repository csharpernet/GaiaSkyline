namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a media asset's old-filename alias row. See ADR 0002.</summary>
public readonly record struct MediaAssetAliasId(Guid Value)
{
    public static MediaAssetAliasId New() => new(Guid.NewGuid());

    public static MediaAssetAliasId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
