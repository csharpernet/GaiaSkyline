namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a detected booking/external-calendar conflict. See ADR 0002.</summary>
public readonly record struct BookingConflictId(Guid Value)
{
    public static BookingConflictId New() => new(Guid.NewGuid());

    public static BookingConflictId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
