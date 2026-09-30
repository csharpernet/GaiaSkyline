namespace GaiaSkyline.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for a partner (e.g. cleaning company, co-host). Reserved for a
/// later stage; defined now so the identity vocabulary is consistent across the domain.
/// See ADR 0002.
/// </summary>
public readonly record struct PartnerId(Guid Value)
{
    public static PartnerId New() => new(Guid.NewGuid());

    public static PartnerId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
