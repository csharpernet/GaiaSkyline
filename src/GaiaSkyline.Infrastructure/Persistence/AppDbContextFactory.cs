using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GaiaSkyline.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by the EF Core tools (<c>dotnet ef</c>) so migrations can be
/// created/applied without booting the Web host. The connection string here is a design-time
/// placeholder — it is never opened when scaffolding migrations.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        const string designTimeConnectionString =
            "Server=localhost;Database=GaiaSkyline;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(designTimeConnectionString)
            .Options;

        return new AppDbContext(options);
    }
}
