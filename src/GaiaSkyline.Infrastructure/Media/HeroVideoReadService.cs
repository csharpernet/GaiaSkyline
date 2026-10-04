using GaiaSkyline.Application.Media;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>Untracked reads for the hero video: the live version (public) and the latest version (admin).</summary>
internal sealed class HeroVideoReadService(AppDbContext dbContext) : IHeroVideoReadService
{
    public async Task<HeroVideoDto?> GetLiveAsync(CancellationToken cancellationToken)
    {
        var hero = await dbContext.HeroVideos
            .AsNoTracking()
            .Include(h => h.Renditions)
            .FirstOrDefaultAsync(h => h.IsLive, cancellationToken);
        if (hero is null || string.IsNullOrWhiteSpace(hero.DesktopPosterBlobUri) || string.IsNullOrWhiteSpace(hero.MobilePosterBlobUri))
        {
            return null;
        }

        return new HeroVideoDto(
            hero.Id.Value,
            hero.Renditions.Select(ToRenditionDto).ToList(),
            hero.DesktopPosterBlobUri,
            hero.DesktopPosterLqip,
            hero.MobilePosterBlobUri,
            hero.MobilePosterLqip,
            hero.LoopDurationSec);
    }

    public async Task<HeroVideoAdminDto?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var hero = await dbContext.HeroVideos
            .AsNoTracking()
            .Include(h => h.Renditions)
            .OrderByDescending(h => h.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (hero is null)
        {
            return null;
        }

        return new HeroVideoAdminDto(
            hero.Id.Value,
            hero.Status,
            hero.IsLive,
            hero.ErrorMessage,
            hero.SourceFileName,
            hero.TrimStartSec,
            hero.TrimEndSec,
            hero.CrossfadeSec,
            hero.FocalX,
            hero.Renditions.OrderBy(r => r.Kind).Select(ToRenditionDto).ToList(),
            hero.DesktopPosterBlobUri,
            hero.MobilePosterBlobUri,
            hero.CreatedAtUtc,
            hero.ReadyAtUtc,
            hero.CreatedBy);
    }

    private static HeroRenditionDto ToRenditionDto(HeroVideoRendition r) =>
        new(r.Kind, r.BlobUri, r.ContentType, r.Width, r.Height, r.ByteSize);
}
