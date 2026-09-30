using Testcontainers.MsSql;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>
/// Spins up a real SQL Server 2022 container for integration tests. We deliberately do NOT use
/// the EF in-memory provider — it does not honour unique constraints, transactions or SQL types
/// we rely on. See ADR 0003.
/// </summary>
/// <remarks>
/// When Docker is unavailable the fixture records a skip reason and tests short-circuit via
/// <c>Skip.IfNot</c> — EXCEPT under CI (the <c>CI</c> env var is set), where a start failure is
/// allowed to surface so integration coverage cannot silently disappear.
/// Note: <see cref="MsSqlBuilder.Build"/> itself probes the Docker endpoint, so it must run
/// inside the guarded block, not in a field initializer.
/// </remarks>
public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private MsSqlContainer? _container;

    public string? ConnectionString { get; private set; }

    public bool IsAvailable => ConnectionString is not null;

    public string SkipReason { get; private set; } = "SQL Server container was not started.";

    public async Task InitializeAsync()
    {
        var runningInCi = Environment.GetEnvironmentVariable("CI") is not null;

        try
        {
            _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }
        catch (Exception ex) when (!runningInCi)
        {
            SkipReason = $"Docker/SQL Server container unavailable: {ex.Message}";
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
