using GaiaSkyline.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class HeroVideoRenditionConfiguration : IEntityTypeConfiguration<HeroVideoRendition>
{
    public void Configure(EntityTypeBuilder<HeroVideoRendition> builder)
    {
        builder.ToTable("HeroVideoRenditions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.HeroVideoId).IsRequired();
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.BlobUri).HasMaxLength(1024).IsRequired();
        builder.Property(r => r.Width).IsRequired();
        builder.Property(r => r.Height).IsRequired();
        builder.Property(r => r.ByteSize).IsRequired();

        builder.Ignore(r => r.ContentType);

        builder.HasIndex(r => new { r.HeroVideoId, r.Kind }).IsUnique();
    }
}
