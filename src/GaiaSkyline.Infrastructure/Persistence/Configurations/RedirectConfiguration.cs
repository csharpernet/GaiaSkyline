using GaiaSkyline.Domain.Seo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class RedirectConfiguration : IEntityTypeConfiguration<Redirect>
{
    public void Configure(EntityTypeBuilder<Redirect> builder)
    {
        builder.ToTable("Redirects");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // FromPath is uniquely indexed, so it must stay within SQL Server's index-key size limit (keep it short).
        builder.Property(r => r.FromPath).HasMaxLength(400).IsRequired();
        builder.Property(r => r.ToPath).HasMaxLength(2048).IsRequired();
        builder.Property(r => r.IsPermanent).IsRequired();
        builder.Property(r => r.CreatedAtUtc).IsRequired();
        builder.Property(r => r.CreatedBy).HasMaxLength(100).IsRequired();

        // One rule per source path.
        builder.HasIndex(r => r.FromPath).IsUnique();
    }
}
