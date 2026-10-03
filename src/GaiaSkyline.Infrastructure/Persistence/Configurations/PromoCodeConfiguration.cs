using GaiaSkyline.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable("PromoCodes");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Code).HasMaxLength(40).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();

        builder.Property(p => p.DiscountPct).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.ValidFrom);
        builder.Property(p => p.ValidUntil);

        // PartnerId is stored but not yet an FK; the Partner aggregate arrives in Stage 8.
        builder.Property(p => p.PartnerId);
    }
}
