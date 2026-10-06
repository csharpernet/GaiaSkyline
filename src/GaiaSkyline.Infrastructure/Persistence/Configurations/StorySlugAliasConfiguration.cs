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
        builder.Property(a => a.LanguageCode).HasMaxLength(5);

        // One owner per old slug and language: a vacated slug redirects to exactly one current story
        // (language null = a canonical rename, which served every language). No filter: SQL Server treats
        // nulls as equal in a unique index, which is exactly right — one canonical alias per old slug.
        builder.HasIndex(a => new { a.OldSlug, a.LanguageCode }).IsUnique().HasFilter(null);
    }
}
