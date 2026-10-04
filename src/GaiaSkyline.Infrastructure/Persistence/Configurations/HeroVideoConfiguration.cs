using GaiaSkyline.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GaiaSkyline.Infrastructure.Persistence.Configurations;

internal sealed class HeroVideoConfiguration : IEntityTypeConfiguration<HeroVideo>
{
    public void Configure(EntityTypeBuilder<HeroVideo> builder)
    {
        builder.ToTable("HeroVideos");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.SourceFileName).HasMaxLength(260).IsRequired();
        builder.Property(h => h.SourceByteSize).IsRequired();
        builder.Property(h => h.SourceDurationSec).IsRequired();
        builder.Property(h => h.TrimStartSec).IsRequired();
        builder.Property(h => h.TrimEndSec).IsRequired();
        builder.Property(h => h.CrossfadeSec).IsRequired();
        builder.Property(h => h.FocalX).IsRequired();

        builder.Property(h => h.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(h => h.IsLive).IsRequired();
        builder.Property(h => h.ErrorMessage).HasMaxLength(2000);

        builder.Property(h => h.DesktopPosterBlobUri).HasMaxLength(1024);
        builder.Property(h => h.DesktopPosterLqip).HasMaxLength(8000);
        builder.Property(h => h.MobilePosterBlobUri).HasMaxLength(1024);
        builder.Property(h => h.MobilePosterLqip).HasMaxLength(8000);

        builder.Property(h => h.CreatedAtUtc).IsRequired();
        builder.Property(h => h.ReadyAtUtc);
        builder.Property(h => h.CreatedBy).HasMaxLength(100).IsRequired();

        // "At most one live version" is enforced in the transcode job (it retires every other live version
        // before promoting the new one in a single transaction); a plain index speeds the live lookup.
        builder.HasIndex(h => h.IsLive);

        builder.Ignore(h => h.LoopDurationSec);

        builder.HasMany(h => h.Renditions)
            .WithOne()
            .HasForeignKey(r => r.HeroVideoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata
            .FindNavigation(nameof(HeroVideo.Renditions))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
