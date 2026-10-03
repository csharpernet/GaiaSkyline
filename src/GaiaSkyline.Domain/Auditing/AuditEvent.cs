using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Auditing;

/// <summary>
/// An append-only audit record. Written for every login (success and failure), every 2FA event, and
/// every Owner write. The actor is the authenticated user id when known (null for anonymous attempts),
/// plus the originating IP. <see cref="DetailsJson"/> holds a small structured payload for context.
/// </summary>
public sealed class AuditEvent
{
    // Required by EF Core's materialization.
    private AuditEvent()
    {
    }

    public AuditEvent(
        AuditEventId id,
        DateTime utcAt,
        Guid? actorUserId,
        string? actorIp,
        string action,
        string? entityType = null,
        string? entityId = null,
        string? detailsJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        Id = id;
        UtcAt = utcAt;
        ActorUserId = actorUserId;
        ActorIp = actorIp;
        Action = action.Trim();
        EntityType = entityType;
        EntityId = entityId;
        DetailsJson = detailsJson;
    }

    public AuditEventId Id { get; private set; }

    public DateTime UtcAt { get; private set; }

    /// <summary>The acting user, when authenticated; null for anonymous attempts (e.g. failed logins).</summary>
    public Guid? ActorUserId { get; private set; }

    public string? ActorIp { get; private set; }

    /// <summary>A stable action code, e.g. "login.success", "2fa.enabled", "content.publish".</summary>
    public string Action { get; private set; } = null!;

    public string? EntityType { get; private set; }

    public string? EntityId { get; private set; }

    public string? DetailsJson { get; private set; }
}
