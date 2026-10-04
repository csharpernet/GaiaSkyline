using GaiaSkyline.Domain.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.ToTable("ContentBlocks");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();

        builder.Property(b => b.Key).HasMaxLength(200).IsRequired();
        builder.HasIndex(b => b.Key).IsUnique();

        builder.Property(b => b.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(b => b.Section).HasMaxLength(100).IsRequired();
        builder.HasIndex(b => b.Section);

        builder.Property(b => b.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(b => b.DisplayOrder).IsRequired();
        builder.Property(b => b.IsPublished).IsRequired();
        builder.Property(b => b.UpdatedAtUtc).IsRequired();
        builder.Property(b => b.UpdatedBy).HasMaxLength(100).IsRequired();
        builder.Ignore(b => b.HasPendingChanges);

        builder.HasMany(b => b.Translations)
            .WithOne()
            .HasForeignKey(t => t.ContentBlockId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata
            .FindNavigation(nameof(ContentBlock.Translations))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
