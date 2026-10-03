using GaiaSkyline.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class StripeEventLogConfiguration : IEntityTypeConfiguration<StripeEventLog>
{
    public void Configure(EntityTypeBuilder<StripeEventLog> builder)
    {
        builder.ToTable("StripeEventLogs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.StripeEventId).HasMaxLength(255).IsRequired();
        builder.HasIndex(e => e.StripeEventId).IsUnique();

        builder.Property(e => e.Type).HasMaxLength(100).IsRequired();
        builder.Property(e => e.PayloadJson).IsRequired();
        builder.Property(e => e.ReceivedAtUtc).IsRequired();
        builder.Property(e => e.ProcessedAtUtc);
        builder.Property(e => e.Error).HasMaxLength(2000);
    }
}
