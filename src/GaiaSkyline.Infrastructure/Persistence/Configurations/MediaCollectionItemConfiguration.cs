using GaiaSkyline.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class MediaCollectionItemConfiguration : IEntityTypeConfiguration<MediaCollectionItem>
{
    public void Configure(EntityTypeBuilder<MediaCollectionItem> builder)
    {
        builder.ToTable("MediaCollectionItems");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.MediaCollectionId).IsRequired();
        builder.Property(i => i.MediaAssetId).IsRequired();
        builder.Property(i => i.DisplayOrder).IsRequired();
        builder.Property(i => i.IsHero).IsRequired();

        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(i => i.MediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.MediaCollectionId, i.MediaAssetId }).IsUnique();
    }
}
