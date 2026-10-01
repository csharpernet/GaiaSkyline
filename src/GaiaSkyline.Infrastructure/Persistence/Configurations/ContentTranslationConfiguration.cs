using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class ContentTranslationConfiguration : IEntityTypeConfiguration<ContentTranslation>
{
    public void Configure(EntityTypeBuilder<ContentTranslation> builder)
    {
        builder.ToTable("ContentTranslations");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.ContentBlockId).IsRequired();
        builder.Property(t => t.LanguageCode).HasMaxLength(5).IsRequired();

        builder.Property(t => t.ValueText); // nvarchar(max), nullable
        builder.Property(t => t.ValueNumber).HasColumnType("decimal(18,4)");
        builder.Property(t => t.ValueBoolean);
        builder.Property(t => t.ValueMediaAssetId);

        builder.Property(t => t.UpdatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedBy).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => new { t.ContentBlockId, t.LanguageCode }).IsUnique();

        // Optional reference to a media asset for ImageRef/VideoRef blocks.
        builder.HasOne<MediaAsset>()
            .WithMany()
            .HasForeignKey(t => t.ValueMediaAssetId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
