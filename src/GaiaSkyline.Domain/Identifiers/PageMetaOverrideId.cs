namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a per-page, per-language SEO meta override. See ADR 0002.</summary>
public readonly record struct PageMetaOverrideId(Guid Value)
{
    public static PageMetaOverrideId New() => new(Guid.NewGuid());

    public static PageMetaOverrideId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
