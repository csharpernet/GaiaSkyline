using GaiaSkyline.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Persistence;

/// <summary>
/// EF Core unit of work for GaiaSkyline. Entity mappings live in
/// <c>Persistence/Configurations</c> and are applied by convention from this assembly.
/// EF-specific types never leak past this project (Clean Architecture boundary).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Property> Properties => Set<Property>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
