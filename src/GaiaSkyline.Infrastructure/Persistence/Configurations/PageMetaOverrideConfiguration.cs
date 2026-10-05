using GaiaSkyline.Domain.Seo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class PageMetaOverrideConfiguration : IEntityTypeConfiguration<PageMetaOverride>
{
    public void Configure(EntityTypeBuilder<PageMetaOverride> builder)
    {
        builder.ToTable("PageMetaOverrides");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.PageKey).HasMaxLength(200).IsRequired();
        builder.Property(p => p.LanguageCode).HasMaxLength(5).IsRequired();
        builder.Property(p => p.Title).HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(400);
        builder.Property(p => p.UpdatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedBy).HasMaxLength(100).IsRequired();

        builder.HasIndex(p => new { p.PageKey, p.LanguageCode }).IsUnique();
    }
}
