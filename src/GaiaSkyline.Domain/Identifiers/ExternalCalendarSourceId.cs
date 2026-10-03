namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a configured external calendar source. See ADR 0002.</summary>
public readonly record struct ExternalCalendarSourceId(Guid Value)
{
    public static ExternalCalendarSourceId New() => new(Guid.NewGuid());

    public static ExternalCalendarSourceId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
