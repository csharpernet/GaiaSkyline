using GaiaSkyline.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.FamilyId).IsRequired();
        builder.Property(e => e.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.ExpiresAtUtc).IsRequired();
        builder.Property(e => e.ConsumedAtUtc);
        builder.Property(e => e.RevokedAtUtc);

        builder.HasIndex(e => e.TokenHash).IsUnique();
        builder.HasIndex(e => e.FamilyId);
    }
}
