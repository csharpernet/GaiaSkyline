using GaiaSkyline.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class MediaAssetAliasConfiguration : IEntityTypeConfiguration<MediaAssetAlias>
{
    public void Configure(EntityTypeBuilder<MediaAssetAlias> builder)
    {
        builder.ToTable("MediaAssetAliases");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.MediaAssetId).IsRequired();
        builder.Property(a => a.OldSlug).HasMaxLength(200).IsRequired();

        // One owner per old filename: a stem redirects to exactly one current asset.
        builder.HasIndex(a => a.OldSlug).IsUnique();
    }
}
