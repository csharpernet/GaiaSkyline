namespace GaiaSkyline.Domain.Identifiers;

/// <summary>
/// Strongly-typed identifier for an <see cref="GaiaSkyline.Domain.Auditing.AuditEvent"/>.
/// See ADR 0002.
/// </summary>
public readonly record struct AuditEventId(Guid Value)
{
    public static AuditEventId New() => new(Guid.NewGuid());

    public static AuditEventId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
