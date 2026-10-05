using GaiaSkyline.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class RateSyncRejectionConfiguration : IEntityTypeConfiguration<RateSyncRejection>
{
    public void Configure(EntityTypeBuilder<RateSyncRejection> builder)
    {
        builder.ToTable("RateSyncRejections");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Date).IsRequired();
        builder.Property(r => r.OfferedPriceEur).HasPrecision(10, 2).IsRequired();
        builder.Property(r => r.Provider).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Reason).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // The sync upserts the single OPEN rejection per date in code; this index just makes the
        // open-rejections review page and that lookup cheap.
        builder.HasIndex(r => new { r.Status, r.Date });
    }
}
