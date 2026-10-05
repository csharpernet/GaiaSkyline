using GaiaSkyline.Domain.Stories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class StorySlugAliasConfiguration : IEntityTypeConfiguration<StorySlugAlias>
{
    public void Configure(EntityTypeBuilder<StorySlugAlias> builder)
    {
        builder.ToTable("StorySlugAliases");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.StoryId).IsRequired();
        builder.Property(a => a.OldSlug).HasMaxLength(200).IsRequired();

        // One owner per old slug: a vacated slug redirects to exactly one current story.
        builder.HasIndex(a => a.OldSlug).IsUnique();
    }
}
