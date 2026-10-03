namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for an imported external-calendar block (populated in Stage 5). See ADR 0002.</summary>
public readonly record struct ExternalCalendarBlockId(Guid Value)
{
    public static ExternalCalendarBlockId New() => new(Guid.NewGuid());

    public static ExternalCalendarBlockId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
