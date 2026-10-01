namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a media asset. See ADR 0002.</summary>
public readonly record struct MediaAssetId(Guid Value)
{
    public static MediaAssetId New() => new(Guid.NewGuid());

    public static MediaAssetId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
