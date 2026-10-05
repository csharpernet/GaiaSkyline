namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a URL redirect rule. See ADR 0002.</summary>
public readonly record struct RedirectId(Guid Value)
{
    public static RedirectId New() => new(Guid.NewGuid());

    public static RedirectId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
