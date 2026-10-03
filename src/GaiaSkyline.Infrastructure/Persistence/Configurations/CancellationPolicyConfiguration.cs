using GaiaSkyline.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class CancellationPolicyConfiguration : IEntityTypeConfiguration<CancellationPolicy>
{
    public void Configure(EntityTypeBuilder<CancellationPolicy> builder)
    {
        builder.ToTable("CancellationPolicies");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Tiers are stored as a JSON document on the singleton row, read/written via the backing field.
        builder.OwnsMany(p => p.Tiers, tiers =>
        {
            tiers.ToJson();
            tiers.Property(t => t.DaysBeforeCheckIn);
            tiers.Property(t => t.RefundPct);
        });
        builder.Navigation(p => p.Tiers).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
