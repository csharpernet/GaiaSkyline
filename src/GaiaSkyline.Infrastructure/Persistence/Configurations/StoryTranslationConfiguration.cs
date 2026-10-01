using GaiaSkyline.Domain.Stories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class StoryTranslationConfiguration : IEntityTypeConfiguration<StoryTranslation>
{
    public void Configure(EntityTypeBuilder<StoryTranslation> builder)
    {
        builder.ToTable("StoryTranslations");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.StoryId).IsRequired();
        builder.Property(t => t.LanguageCode).HasMaxLength(5).IsRequired();
        builder.Property(t => t.Title).HasMaxLength(300).IsRequired();
        builder.Property(t => t.Excerpt).HasMaxLength(1000).IsRequired();
        builder.Property(t => t.BodyRichText).IsRequired(); // nvarchar(max)
        builder.Property(t => t.MetaTitle).HasMaxLength(200);
        builder.Property(t => t.MetaDescription).HasMaxLength(400);
        builder.Property(t => t.ReadingTimeMinutes).IsRequired();

        builder.HasIndex(t => new { t.StoryId, t.LanguageCode }).IsUnique();
    }
}
