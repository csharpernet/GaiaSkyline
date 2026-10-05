using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Stories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class StoryConfiguration : IEntityTypeConfiguration<Story>
{
    public void Configure(EntityTypeBuilder<Story> builder)
    {
        builder.ToTable("Stories");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Slug).HasMaxLength(200).IsRequired();
        builder.HasIndex(s => s.Slug).IsUnique();

        builder.Property(s => s.CoverMediaAssetId).IsRequired();
        builder.Property(s => s.PublishedAtUtc).IsRequired();
        builder.Property(s => s.IsPublished).IsRequired();
        builder.Property(s => s.DisplayOrder).IsRequired();
        builder.Property(s => s.AuthorName).HasMaxLength(200).IsRequired();

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(s => s.CoverMediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Translations)
            .WithOne()
            .HasForeignKey(t => t.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata
            .FindNavigation(nameof(Story.Translations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(s => s.Aliases)
            .WithOne()
            .HasForeignKey(a => a.StoryId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata
            .FindNavigation(nameof(Story.Aliases))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
