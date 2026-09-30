namespace GaiaSkyline.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a booking. Reserved for a later stage; defined now so the
/// identity vocabulary is consistent across the domain. See ADR 0002.
/// </summary>
public readonly record struct BookingId(Guid Value)
{
    public static BookingId New() => new(Guid.NewGuid());

    public static BookingId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
