using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// Boots the real app against a unique, throwaway SQL Server LocalDB database that is migrated and
/// seeded on startup, then dropped on dispose. Used for the public-site end-to-end tests.
/// </summary>
public sealed class PublicSiteFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"GaiaSkyline_Web_{Guid.NewGuid():N}";

    private string ConnectionString =>
        $@"Server=(localdb)\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Features:SeedContentOnStartup"] = "true",
                // Keep the Hangfire server/schema out of the web tests; job logic is tested directly.
                ["BackgroundJobs:Enabled"] = "false",
            }));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var scope = Services.CreateScope();
                scope.ServiceProvider.GetService<AppDbContext>()?.Database.EnsureDeleted();
            }
            catch
            {
                // Best effort — the throwaway database is uniquely named.
            }
        }

        base.Dispose(disposing);
    }
}
