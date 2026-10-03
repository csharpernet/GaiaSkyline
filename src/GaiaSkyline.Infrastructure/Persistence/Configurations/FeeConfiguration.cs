using GaiaSkyline.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class FeeConfiguration : IEntityTypeConfiguration<Fee>
{
    public void Configure(EntityTypeBuilder<Fee> builder)
    {
        builder.ToTable("Fees");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Amount).IsRequired();
        builder.Property(f => f.IsPerNight).IsRequired();
        builder.Property(f => f.IsPerGuest).IsRequired();
        builder.Property(f => f.MaxNights);
        builder.Property(f => f.MinAgeExempt);
    }
}
