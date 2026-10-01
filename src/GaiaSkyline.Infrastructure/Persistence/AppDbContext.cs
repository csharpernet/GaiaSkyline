using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Domain.Stories;
using GaiaSkyline.Infrastructure.Persistence.Converters;
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

    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();

    public DbSet<ContentTranslation> ContentTranslations => Set<ContentTranslation>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();

    public DbSet<MediaCollection> MediaCollections => Set<MediaCollection>();

    public DbSet<MediaCollectionItem> MediaCollectionItems => Set<MediaCollectionItem>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<Story> Stories => Set<Story>();

    public DbSet<StoryTranslation> StoryTranslations => Set<StoryTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Strongly-typed ids introduced in Stage 2 (covers both TId and nullable TId? properties).
        configurationBuilder.Properties<ContentBlockId>().HaveConversion<ContentBlockIdConverter>();
        configurationBuilder.Properties<ContentTranslationId>().HaveConversion<ContentTranslationIdConverter>();
        configurationBuilder.Properties<MediaAssetId>().HaveConversion<MediaAssetIdConverter>();
        configurationBuilder.Properties<MediaCollectionId>().HaveConversion<MediaCollectionIdConverter>();
        configurationBuilder.Properties<MediaCollectionItemId>().HaveConversion<MediaCollectionItemIdConverter>();
        configurationBuilder.Properties<ReviewId>().HaveConversion<ReviewIdConverter>();
        configurationBuilder.Properties<StoryId>().HaveConversion<StoryIdConverter>();
        configurationBuilder.Properties<StoryTranslationId>().HaveConversion<StoryTranslationIdConverter>();
    }
}
