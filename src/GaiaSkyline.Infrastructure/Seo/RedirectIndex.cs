using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Seo;
using GaiaSkyline.Domain.Seo;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Seo;

/// <summary>
/// In-memory index of redirect rules for the before-routing middleware. Loaded on first use and reloaded only
/// when the content revision changes (adds/deletes bump it), so the common case is a dictionary lookup with no
/// database hit on the request hot path. Singleton. Stage 7 §5.
/// </summary>
internal sealed class RedirectIndex(
    IServiceScopeFactory scopeFactory,
    IContentRevision revision,
    ILogger<RedirectIndex> logger) : IRedirectResolver, IDisposable
{
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private volatile Dictionary<string, RedirectTarget> _map = new(StringComparer.Ordinal);
    private long _loadedRevision = -1;

    public void Dispose() => _reloadGate.Dispose();

    public async Task<RedirectTarget?> ResolveAsync(string path, CancellationToken cancellationToken)
    {
        var from = Redirect.NormalizeFrom(path);
        if (from is null)
        {
            return null;
        }

        await EnsureLoadedAsync(cancellationToken);
        return _map.TryGetValue(from, out var target) ? target : null;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loadedRevision == revision.Current)
        {
            return;
        }

        await _reloadGate.WaitAsync(cancellationToken);
        try
        {
            var current = revision.Current;
            if (_loadedRevision == current)
            {
                return;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var all = await dbContext.Redirects.AsNoTracking().ToListAsync(cancellationToken);
                _map = all.ToDictionary(r => r.FromPath, r => new RedirectTarget(r.ToPath, r.IsPermanent), StringComparer.Ordinal);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Never take the whole site down because redirect rules could not be loaded (e.g. the DB is
                // briefly unavailable, or a host that opts out of migrations). Serve without redirects.
                _map = new Dictionary<string, RedirectTarget>(StringComparer.Ordinal);
                logger.LogWarning(ex, "Could not load redirect rules; serving without redirects until the next change.");
            }

            _loadedRevision = current;
        }
        finally
        {
            _reloadGate.Release();
        }
    }
}
