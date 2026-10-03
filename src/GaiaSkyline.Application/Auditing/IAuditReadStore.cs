namespace GaiaSkyline.Application.Auditing;

/// <summary>A single audit row for display, with the actor's email resolved where known.</summary>
public sealed record AuditLogEntry(
    DateTime UtcAt,
    string Action,
    Guid? ActorUserId,
    string? ActorEmail,
    string? ActorIp,
    string? EntityType,
    string? EntityId,
    string? DetailsJson);

/// <summary>Filter + paging for the audit log. Empty filters return everything, newest first.</summary>
public sealed record AuditLogQuery(
    string? Action = null,
    string? EntityType = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>A page of audit rows plus the distinct actions/entity types for the filter dropdowns.</summary>
public sealed record AuditLogPage(
    IReadOnlyList<AuditLogEntry> Items,
    int TotalCount,
    int Page,
    int PageSize,
    IReadOnlyList<string> Actions,
    IReadOnlyList<string> EntityTypes);

/// <summary>Read-only access to the append-only audit trail for the admin audit page.</summary>
public interface IAuditReadStore
{
    Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken);
}
