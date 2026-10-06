using GaiaSkyline.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="BookingDocument"/> PDF cache (Stage 8). One row per booking reference, document type
/// and language — the unique index is both the lookup key and the guard against a duplicate cache entry.
/// </summary>
internal sealed class BookingDocumentConfiguration : IEntityTypeConfiguration<BookingDocument>
{
    public void Configure(EntityTypeBuilder<BookingDocument> builder)
    {
        builder.ToTable("BookingDocuments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.BookingReference).HasMaxLength(32).IsRequired();
        builder.Property(d => d.DocumentType).HasMaxLength(40).IsRequired();
        builder.Property(d => d.Language).HasMaxLength(16).IsRequired();
        builder.Property(d => d.SourceHash).HasMaxLength(64).IsRequired();
        builder.Property(d => d.Content).IsRequired();
        builder.Property(d => d.CreatedAtUtc).IsRequired();

        builder.HasIndex(d => new { d.BookingReference, d.DocumentType, d.Language }).IsUnique();
    }
}
