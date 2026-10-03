using GaiaSkyline.Domain.Availability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class BookingConflictConfiguration : IEntityTypeConfiguration<BookingConflict>
{
    public void Configure(EntityTypeBuilder<BookingConflict> builder)
    {
        builder.ToTable("BookingConflicts");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.BookingReference).HasMaxLength(32).IsRequired();
        builder.Property(c => c.SourceName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.StartDate).IsRequired();
        builder.Property(c => c.EndDate).IsRequired();
        builder.Property(c => c.DetectedAtUtc).IsRequired();

        // Record a given conflict once (per booking + source + range).
        builder.HasIndex(c => new { c.BookingReference, c.SourceName, c.StartDate, c.EndDate }).IsUnique();
    }
}
