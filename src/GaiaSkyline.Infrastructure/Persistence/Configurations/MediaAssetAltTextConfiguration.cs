using GaiaSkyline.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class MediaAssetAltTextConfiguration : IEntityTypeConfiguration<MediaAssetAltText>
{
    public void Configure(EntityTypeBuilder<MediaAssetAltText> builder)
    {
        builder.ToTable("MediaAssetAltTexts");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.MediaAssetId).IsRequired();
        builder.Property(t => t.LanguageCode).HasMaxLength(5).IsRequired();
        builder.Property(t => t.Text).HasMaxLength(500).IsRequired();

        builder.HasIndex(t => new { t.MediaAssetId, t.LanguageCode }).IsUnique();
    }
}
