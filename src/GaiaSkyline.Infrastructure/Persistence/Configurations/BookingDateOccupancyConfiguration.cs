using GaiaSkyline.Domain.Bookings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class BookingDateOccupancyConfiguration : IEntityTypeConfiguration<BookingDateOccupancy>
{
    public void Configure(EntityTypeBuilder<BookingDateOccupancy> builder)
    {
        builder.ToTable("BookingDateOccupancies");

        // The night is the primary key — a conflicting insert fails, which is the double-booking guard.
        builder.HasKey(o => o.Date);
        builder.Property(o => o.Date).ValueGeneratedNever();

        builder.Property(o => o.BookingId).IsRequired();
        builder.HasIndex(o => o.BookingId);

        builder.HasOne<Booking>()
            .WithMany()
            .HasForeignKey(o => o.BookingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
