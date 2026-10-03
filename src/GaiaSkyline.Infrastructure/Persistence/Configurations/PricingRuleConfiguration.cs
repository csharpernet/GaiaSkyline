using GaiaSkyline.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class PricingRuleConfiguration : IEntityTypeConfiguration<PricingRule>
{
    public void Configure(EntityTypeBuilder<PricingRule> builder)
    {
        builder.ToTable("PricingRules");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.StartDate).IsRequired();
        builder.Property(r => r.EndDate).IsRequired();
        builder.Property(r => r.NightlyRate).IsRequired();
        builder.Property(r => r.MinNights).IsRequired();
        builder.Property(r => r.WeeklyDiscountPct).IsRequired();
        builder.Property(r => r.MonthlyDiscountPct).IsRequired();

        builder.HasIndex(r => new { r.StartDate, r.EndDate });
    }
}
