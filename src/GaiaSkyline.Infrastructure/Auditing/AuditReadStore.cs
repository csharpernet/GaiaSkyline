using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Infrastructure.Identity;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Auditing;

/// <summary>EF Core read model for the audit trail. Untracked; resolves the actor's email by left-join.</summary>
internal sealed class AuditReadStore(AppDbContext dbContext) : IAuditReadStore
{
    public async Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = dbContext.AuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            filtered = filtered.Where(a => a.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            filtered = filtered.Where(a => a.EntityType == query.EntityType);
        }

        if (query.EntityIds is { Count: > 0 } entityIds)
        {
            filtered = filtered.Where(a => a.EntityId != null && entityIds.Contains(a.EntityId));
        }

        if (query.From is { } from)
        {
            var fromUtc = from.ToDateTime(TimeOnly.MinValue);
            filtered = filtered.Where(a => a.UtcAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toExclusiveUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
            filtered = filtered.Where(a => a.UtcAt < toExclusiveUtc);
        }

        var total = await filtered.CountAsync(cancellationToken);

        var items = await (
                from a in filtered
                join u in dbContext.Set<ApplicationUser>() on a.ActorUserId equals (Guid?)u.Id into actor
                from u in actor.DefaultIfEmpty()
                orderby a.UtcAt descending
                select new AuditLogEntry(
                    a.UtcAt, a.Action, a.ActorUserId, u != null ? u.Email : null,
                    a.ActorIp, a.EntityType, a.EntityId, a.DetailsJson))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var actions = await dbContext.AuditEvents.AsNoTracking()
            .Select(a => a.Action).Distinct().OrderBy(a => a).Take(200).ToListAsync(cancellationToken);
        var entityTypes = await dbContext.AuditEvents.AsNoTracking()
            .Where(a => a.EntityType != null).Select(a => a.EntityType!).Distinct().OrderBy(a => a).Take(200)
            .ToListAsync(cancellationToken);

        return new AuditLogPage(items, total, page, pageSize, actions, entityTypes);
    }
}
