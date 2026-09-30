namespace GaiaSkyline.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a <see cref="Entities.Property"/>.
/// Backed by a <see cref="Guid"/>; see ADR 0002 for why we use readonly record structs.
/// </summary>
public readonly record struct PropertyId(Guid Value)
{
    /// <summary>Create a brand-new random identifier.</summary>
    public static PropertyId New() => new(Guid.NewGuid());

    /// <summary>Wrap an existing <see cref="Guid"/> value.</summary>
    public static PropertyId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
