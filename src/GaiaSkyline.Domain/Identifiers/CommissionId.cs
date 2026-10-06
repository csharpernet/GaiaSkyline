namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a partner commission (Stage 8 Part A).</summary>
public readonly record struct CommissionId(Guid Value)
{
    public static CommissionId New() => new(Guid.NewGuid());

    public static CommissionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
