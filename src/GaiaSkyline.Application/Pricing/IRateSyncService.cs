namespace GaiaSkyline.Application.Pricing;

/// <summary>
/// Syncs per-date rates from the configured provider for the next 18 months. With no provider
/// configured it logs once and exits (manual pricing mode — not a failure). Driven by a Hangfire job.
/// </summary>
public interface IRateSyncService
{
    Task SyncAsync(CancellationToken cancellationToken);
}
