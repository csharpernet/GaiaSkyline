using GaiaSkyline.Domain.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class ExternalCalendarBlockConfiguration : IEntityTypeConfiguration<ExternalCalendarBlock>
{
    public void Configure(EntityTypeBuilder<ExternalCalendarBlock> builder)
    {
        builder.ToTable("ExternalCalendarBlocks");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Source).HasMaxLength(50).IsRequired();
        builder.Property(b => b.StartDate).IsRequired();
        builder.Property(b => b.EndDate).IsRequired();
        builder.Property(b => b.ExternalUid).HasMaxLength(255);
        builder.Property(b => b.Summary).HasMaxLength(500);
        builder.Property(b => b.IsActive).IsRequired();
        builder.Property(b => b.CreatedAtUtc).IsRequired();
        builder.Property(b => b.LastSeenAtUtc).IsRequired();

        // The availability query filters active blocks overlapping a date window.
        builder.HasIndex(b => new { b.IsActive, b.StartDate, b.EndDate });
        builder.HasIndex(b => b.ExternalUid);
    }
}
