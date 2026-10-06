using GaiaSkyline.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("MediaAssets");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(a => a.BlobUri).HasMaxLength(1024).IsRequired();
        builder.Property(a => a.PosterBlobUri).HasMaxLength(1024);
        builder.Property(a => a.Width).IsRequired();
        builder.Property(a => a.Height).IsRequired();
        builder.Property(a => a.DurationSec);
        builder.Property(a => a.ByteSize).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.UploadedAtUtc).IsRequired();
        builder.Property(a => a.UploadedBy).HasMaxLength(100).IsRequired();
        // Cache-busting token (Stage 8 Part B); existing rows default to 1, meaning "no ?v= yet".
        builder.Property(a => a.Version).IsRequired().HasDefaultValue(1);
        builder.Property(a => a.AltText).HasMaxLength(500);
        // LQIP is a base64 data: URI for a ~24px-wide blurred preview; a few KB at most.
        builder.Property(a => a.Lqip).HasMaxLength(8000);

        builder.Property(a => a.IsDeleted).IsRequired();
        builder.Property(a => a.DeletedAtUtc);
        builder.HasIndex(a => a.IsDeleted);

        builder.HasMany(a => a.AltTexts)
            .WithOne()
            .HasForeignKey(t => t.MediaAssetId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata
            .FindNavigation(nameof(MediaAsset.AltTexts))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.Aliases)
            .WithOne()
            .HasForeignKey(a => a.MediaAssetId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata
            .FindNavigation(nameof(MediaAsset.Aliases))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
