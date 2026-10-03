using System.Text.Json;
using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Domain.Auditing;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Auditing;

/// <summary>
/// Persists audit records. A write failure is logged but never surfaced to the caller — auditing must
/// not break a login or an owner write.
/// </summary>
internal sealed class AuditLog(AppDbContext dbContext, TimeProvider clock, ILogger<AuditLog> logger) : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(
        string action,
        Guid? actorUserId,
        string? actorIp,
        string? entityType = null,
        string? entityId = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        var detailsJson = details is null ? null : JsonSerializer.Serialize(details, JsonOptions);
        var record = new AuditEvent(
            AuditEventId.New(),
            clock.GetUtcNow().UtcDateTime,
            actorUserId,
            actorIp,
            action,
            entityType,
            entityId,
            detailsJson);

        try
        {
            dbContext.AuditEvents.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Failed to write audit event {Action}.", action);
        }
    }
}
