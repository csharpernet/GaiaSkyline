using GaiaSkyline.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class SiteSettingConfiguration : IEntityTypeConfiguration<SiteSetting>
{
    public void Configure(EntityTypeBuilder<SiteSetting> builder)
    {
        builder.ToTable("SiteSettings");

        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(100).ValueGeneratedNever();
        builder.Property(s => s.Value).IsRequired();
        builder.Property(s => s.IsSecret).IsRequired();
    }
}
