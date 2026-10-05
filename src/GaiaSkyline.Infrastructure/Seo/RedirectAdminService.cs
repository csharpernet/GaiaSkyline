using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Seo;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Seo;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Seo;

/// <summary>Owner-only redirect management: list, add (deduped + loop-checked) and delete. Stage 7 §5.</summary>
internal sealed class RedirectAdminService(
    AppDbContext dbContext,
    IContentRevision revision,
    TimeProvider clock) : IRedirectAdminService
{
    private const int MaxChain = 25;

    public async Task<IReadOnlyList<RedirectDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.Redirects.AsNoTracking().OrderBy(r => r.FromPath).ToListAsync(cancellationToken);
        return rows.Select(r => new RedirectDto(r.Id.Value, r.FromPath, r.ToPath, r.IsPermanent, r.CreatedAtUtc)).ToList();
    }

    public async Task<RedirectAddResult> AddAsync(string fromPath, string toPath, bool permanent, string actor, CancellationToken cancellationToken)
    {
        var from = Redirect.NormalizeFrom(fromPath);
        var to = Redirect.NormalizeTo(toPath);
        if (from is null)
        {
            return RedirectAddResult.Fail("The from-path must be site-relative and start with '/'.");
        }

        if (to is null)
        {
            return RedirectAddResult.Fail("The to-path must be site-relative and start with '/'.");
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectAddResult.Fail("A redirect cannot point a path at itself.");
        }

        var existing = await dbContext.Redirects.AsNoTracking().ToListAsync(cancellationToken);
        if (existing.Any(r => string.Equals(r.FromPath, from, StringComparison.OrdinalIgnoreCase)))
        {
            return RedirectAddResult.Fail($"A redirect for '{from}' already exists.");
        }

        if (CreatesLoop(from, to, existing))
        {
            return RedirectAddResult.Fail("That would create a redirect loop.");
        }

        var redirect = new Redirect(RedirectId.New(), from, to, permanent, clock.GetUtcNow().UtcDateTime, actor);
        dbContext.Redirects.Add(redirect);
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return RedirectAddResult.Success(redirect.Id.Value);
    }

    public async Task<bool> DeleteAsync(Guid id, string actor, CancellationToken cancellationToken)
    {
        var redirectId = RedirectId.From(id);
        var redirect = await dbContext.Redirects.FirstOrDefaultAsync(r => r.Id == redirectId, cancellationToken);
        if (redirect is null)
        {
            return false;
        }

        dbContext.Redirects.Remove(redirect);
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }

    // Follow the chain from the new rule's destination through the existing rules; if it comes back to the new
    // from-path (or cycles among existing rules / runs too long), adding the rule would loop.
    private static bool CreatesLoop(string from, string to, IReadOnlyCollection<Redirect> existing)
    {
        var map = existing.ToDictionary(r => r.FromPath, r => r.ToPath, StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = Redirect.NormalizeFrom(to);

        for (var hops = 0; current is not null && hops < MaxChain; hops++)
        {
            if (string.Equals(current, from, StringComparison.OrdinalIgnoreCase))
            {
                return true; // the chain returns to the new source
            }

            if (!visited.Add(current))
            {
                return true; // a cycle among existing rules
            }

            if (!map.TryGetValue(current, out var next))
            {
                return false; // the chain ends at a non-redirected path
            }

            current = Redirect.NormalizeFrom(next);
        }

        return current is not null; // ran the whole budget and is still redirecting → treat as a loop
    }
}
