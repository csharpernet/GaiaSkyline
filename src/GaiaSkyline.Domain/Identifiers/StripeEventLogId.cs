namespace GaiaSkyline.Domain.Identifiers;

/// <summary>Strongly-typed identifier for a logged Stripe webhook event. See ADR 0002.</summary>
public readonly record struct StripeEventLogId(Guid Value)
{
    public static StripeEventLogId New() => new(Guid.NewGuid());

    public static StripeEventLogId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
