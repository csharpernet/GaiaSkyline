using GaiaSkyline.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class DailyRateConfiguration : IEntityTypeConfiguration<DailyRate>
{
    public void Configure(EntityTypeBuilder<DailyRate> builder)
    {
        builder.ToTable("DailyRates");

        // The date is the natural key (one rate per calendar day).
        builder.HasKey(d => d.Date);
        builder.Property(d => d.Date).ValueGeneratedNever();

        builder.Property(d => d.NightlyRate).IsRequired();
        builder.Property(d => d.MinNights);
        builder.Property(d => d.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(d => d.SourceUpdatedAtUtc);
        builder.Property(d => d.IsLockedByOwner).IsRequired();
        builder.Property(d => d.UpdatedAtUtc).IsRequired();
        builder.Property(d => d.UpdatedBy).HasMaxLength(256).IsRequired();
    }
}
