namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for the (singleton) cancellation policy. See ADR 0002.</summary>
public readonly record struct CancellationPolicyId(Guid Value)
{
    public static CancellationPolicyId New() => new(Guid.NewGuid());

    public static CancellationPolicyId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
