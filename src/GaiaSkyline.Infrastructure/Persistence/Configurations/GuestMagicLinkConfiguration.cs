using GaiaSkyline.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class GuestMagicLinkConfiguration : IEntityTypeConfiguration<GuestMagicLink>
{
    public void Configure(EntityTypeBuilder<GuestMagicLink> builder)
    {
        builder.ToTable("GuestMagicLinks");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.BookingReference).HasMaxLength(32).IsRequired();
        builder.Property(e => e.CreatedAtUtc).IsRequired();
        builder.Property(e => e.ExpiresAtUtc).IsRequired();
        builder.Property(e => e.ConsumedAtUtc);

        builder.HasIndex(e => e.BookingReference);
    }
}
