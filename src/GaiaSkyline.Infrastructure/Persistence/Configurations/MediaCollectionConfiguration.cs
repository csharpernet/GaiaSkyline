using GaiaSkyline.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class MediaCollectionConfiguration : IEntityTypeConfiguration<MediaCollection>
{
    public void Configure(EntityTypeBuilder<MediaCollection> builder)
    {
        builder.ToTable("MediaCollections");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Key).HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.Key).IsUnique();
        builder.Property(c => c.DisplayName).HasMaxLength(200).IsRequired();

        builder.HasMany(c => c.Items)
            .WithOne()
            .HasForeignKey(i => i.MediaCollectionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata
            .FindNavigation(nameof(MediaCollection.Items))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
