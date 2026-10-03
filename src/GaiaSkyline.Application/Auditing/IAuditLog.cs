namespace GaiaSkyline.Application.Auditing;

/// <summary>
/// Writes append-only audit records. Implementations serialize <paramref name="details"/> to JSON and
/// never throw into the caller's flow (auditing must not break a login or a write).
/// </summary>
public interface IAuditLog
{
    Task WriteAsync(
        string action,
        Guid? actorUserId,
        string? actorIp,
        string? entityType = null,
        string? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default);
}
