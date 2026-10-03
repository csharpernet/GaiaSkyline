using GaiaSkyline.Domain.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class OwnerBlockConfiguration : IEntityTypeConfiguration<OwnerBlock>
{
    public void Configure(EntityTypeBuilder<OwnerBlock> builder)
    {
        builder.ToTable("OwnerBlocks");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.EndDate).IsRequired();
        builder.Property(e => e.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.Note).HasMaxLength(500);
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.CreatedBy).HasMaxLength(256).IsRequired();

        builder.HasIndex(e => new { e.StartDate, e.EndDate });
    }
}
