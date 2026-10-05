using GaiaSkyline.Application.Content;
using GaiaSkyline.Application.Seo;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Seo;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Seo;

/// <summary>Owner management of per-page, per-language SEO meta overrides. Stage 7 §5.</summary>
internal sealed class PageMetaAdminService(
    AppDbContext dbContext,
    IContentRevision revision,
    TimeProvider clock) : IPageMetaAdminService
{
    public async Task<IReadOnlyList<PageMetaOverrideDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await dbContext.PageMetaOverrides.AsNoTracking().ToListAsync(cancellationToken);
        return rows.Select(p => new PageMetaOverrideDto(p.PageKey, p.LanguageCode, p.Title, p.Description, p.NoIndex, p.NoFollow)).ToList();
    }

    public async Task<bool> UpsertAsync(
        string pageKey, string languageCode, string? title, string? description, bool noIndex, bool noFollow,
        string actor, CancellationToken cancellationToken)
    {
        var key = (pageKey ?? string.Empty).Trim().ToLowerInvariant();
        if (!SeoPages.IsKnown(key) || !ContentLanguages.All.Contains(languageCode))
        {
            return false;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var existing = await dbContext.PageMetaOverrides
            .FirstOrDefaultAsync(p => p.PageKey == key && p.LanguageCode == languageCode, cancellationToken);
        // Nothing but defaults (no title/description, index + follow) → no row needed.
        var isDefault = string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description) && !noIndex && !noFollow;

        if (existing is null)
        {
            if (isDefault)
            {
                return true; // nothing to store
            }

            dbContext.PageMetaOverrides.Add(
                new PageMetaOverride(PageMetaOverrideId.New(), key, languageCode, title, description, noIndex, noFollow, now, actor));
        }
        else if (isDefault)
        {
            dbContext.PageMetaOverrides.Remove(existing);
        }
        else
        {
            existing.Set(title, description, noIndex, noFollow, now, actor);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }
}
