using GaiaSkyline.Domain.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class ExternalCalendarSourceConfiguration : IEntityTypeConfiguration<ExternalCalendarSource>
{
    public void Configure(EntityTypeBuilder<ExternalCalendarSource> builder)
    {
        builder.ToTable("ExternalCalendarSources");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => s.Name).IsUnique();

        builder.Property(s => s.IcsUrlProtected).IsRequired();
        builder.Property(s => s.IsEnabled).IsRequired();
        builder.Property(s => s.LastHash).HasMaxLength(128);
        builder.Property(s => s.LastSuccessUtc);
        builder.Property(s => s.LastError).HasMaxLength(2000);
        builder.Property(s => s.ConsecutiveFailures).IsRequired();
        builder.Property(s => s.CreatedAtUtc).IsRequired();
    }
}
