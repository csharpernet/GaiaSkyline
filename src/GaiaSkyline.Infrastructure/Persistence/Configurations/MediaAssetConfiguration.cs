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
        builder.Property(a => a.AltText).HasMaxLength(500);
    }
}
