namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a rate-sync rejection. See ADR 0002.</summary>
public readonly record struct RateSyncRejectionId(Guid Value)
{
    public static RateSyncRejectionId New() => new(Guid.NewGuid());

    public static RateSyncRejectionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
