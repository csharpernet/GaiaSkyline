using System.Collections.Concurrent;
using GaiaSkyline.Application.Settings;
using GaiaSkyline.Domain.Settings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Settings;

/// <summary>Protects secret setting values at rest (same Data Protection approach as calendar URLs).</summary>
public sealed class SiteSettingsProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("GaiaSkyline.SiteSettings.v1");

    public string Protect(string value) => _protector.Protect(value);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}

/// <summary>
/// Singleton snapshot of the SiteSettings table (Stage 7 §12). Loaded lazily via a scope (the store is
/// consulted from options post-configuration, which must stay synchronous), invalidated by
/// <see cref="Reload"/> after every write. A load failure degrades to "no overrides" — configuration
/// defaults then apply, and the site keeps serving.
/// </summary>
public sealed class SiteSettingsSnapshot(
    IServiceScopeFactory scopeFactory,
    SiteSettingsProtector protector,
    ILogger<SiteSettingsSnapshot> logger) : ISiteSettings
{
    private readonly object _gate = new();
    private ConcurrentDictionary<string, (string Value, bool IsSecret)>? _snapshot;

    public string? Get(string key)
    {
        var row = Row(key);
        if (row is null)
        {
            return null;
        }

        if (!row.Value.IsSecret)
        {
            return row.Value.Value;
        }

        try
        {
            return protector.Unprotect(row.Value.Value);
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException)
        {
            logger.LogWarning(ex, "Setting {Key} could not be unprotected; treating as unset.", key);
            return null;
        }
    }

    public string? GetMasked(string key)
    {
        var value = Get(key);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var row = Row(key);
        if (row is { } r && !r.IsSecret)
        {
            return value;
        }

        return value.Length <= 4 ? "••••" : $"••••{value[^4..]}";
    }

    public void Reload()
    {
        lock (_gate)
        {
            _snapshot = null;
        }
    }

    private (string Value, bool IsSecret)? Row(string key)
    {
        var snapshot = _snapshot;
        if (snapshot is null)
        {
            lock (_gate)
            {
                snapshot = _snapshot ??= Load();
            }
        }

        return snapshot.TryGetValue(key, out var row) ? row : null;
    }

    private ConcurrentDictionary<string, (string, bool)> Load()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = dbContext.SiteSettings.AsNoTracking().ToList();
            return new ConcurrentDictionary<string, (string, bool)>(
                rows.ToDictionary(r => r.Key, r => (r.Value, r.IsSecret)));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            logger.LogWarning(ex, "Site settings could not be loaded; configuration defaults apply.");
            return new ConcurrentDictionary<string, (string, bool)>();
        }
    }
}

/// <summary>Scoped write side: upsert/delete a row, then drop the singleton snapshot.</summary>
internal sealed class SiteSettingsWriter(
    AppDbContext dbContext,
    ISiteSettings snapshot,
    SiteSettingsProtector protector,
    TimeProvider clock) : ISiteSettingsWriter
{
    public async Task SetAsync(string key, string? value, bool isSecret, CancellationToken cancellationToken)
    {
        var existing = await dbContext.SiteSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (string.IsNullOrWhiteSpace(value))
        {
            if (existing is not null)
            {
                dbContext.SiteSettings.Remove(existing);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            snapshot.Reload();
            return;
        }

        var stored = isSecret ? protector.Protect(value.Trim()) : value.Trim();
        var now = clock.GetUtcNow().UtcDateTime;
        if (existing is null)
        {
            dbContext.SiteSettings.Add(new SiteSetting(key, stored, isSecret, now));
        }
        else
        {
            existing.Set(stored, isSecret, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        snapshot.Reload();
    }
}
