using GaiaSkyline.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("AuditEvents");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.UtcAt).IsRequired();
        builder.Property(e => e.ActorUserId);
        builder.Property(e => e.ActorIp).HasMaxLength(64);
        builder.Property(e => e.Action).HasMaxLength(128).IsRequired();
        builder.Property(e => e.EntityType).HasMaxLength(128);
        builder.Property(e => e.EntityId).HasMaxLength(256);
        builder.Property(e => e.DetailsJson);

        builder.HasIndex(e => e.UtcAt);
        builder.HasIndex(e => e.ActorUserId);
        builder.HasIndex(e => e.Action);
    }
}
