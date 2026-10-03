using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>
/// Provisions a throwaway SQL Server LocalDB database for integration tests: a uniquely-named
/// database is created and migrated on construction, and dropped on dispose. See ADR 0004.
/// </summary>
/// <remarks>
/// Consume this as an <see cref="IClassFixture{T}"/>: xUnit then creates one instance per test
/// class, so every class gets its OWN database (isolated) while all tests within a class share it.
/// A single shared collection fixture would instead hand every class the same database and defeat
/// that isolation, so a class fixture is the deliberate choice here.
/// </remarks>
public sealed class LocalDbFixture : IDisposable
{
    public LocalDbFixture()
    {
        EnsureLocalDbIsInstalled();

        var databaseName = $"GaiaSkyline_Tests_{Guid.NewGuid():N}";
        ConnectionString =
            $@"Server=(localdb)\mssqllocaldb;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        using var context = CreateContext();
        context.Database.Migrate(); // creates the database and applies 0001_Init
    }

    public string ConnectionString { get; }

    public AppDbContext CreateContext()
    {
        // Mirror production (see Infrastructure.DependencyInjection): retry on transient failures, so
        // the Serializable booking transaction retries a deadlock victim into a clean domain result.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString, sql => sql.EnableRetryOnFailure())
            .Options;
        return new AppDbContext(options);
    }

    public void Dispose()
    {
        // Release pooled connections so the unique test database can be dropped.
        SqlConnection.ClearAllPools();
        using var context = CreateContext();
        context.Database.EnsureDeleted();
        GC.SuppressFinalize(this);
    }

    private static void EnsureLocalDbIsInstalled()
    {
        const string masterConnectionString =
            @"Server=(localdb)\mssqllocaldb;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

        try
        {
            using var connection = new SqlConnection(masterConnectionString);
            connection.Open();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "SQL Server LocalDB is required to run the integration tests but the instance " +
                @"'(localdb)\mssqllocaldb' could not be reached. Install it via the Visual Studio " +
                "Installer (the \"Data storage and processing\" workload) or the standalone " +
                "SqlLocalDB MSI, then run:  sqllocaldb start mssqllocaldb" +
                $"{Environment.NewLine}Underlying error: {ex.Message}", ex);
        }
    }
}
