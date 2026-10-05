using GaiaSkyline.Application.Seo;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Seo;

/// <summary>
/// Reads a page's SEO meta override for a language (exact match). Called only on an output-cache miss when a
/// public page renders. Resilient — returns null on any read failure so meta falls back to the page default.
/// Stage 7 §5.
/// </summary>
internal sealed class PageMetaResolver(AppDbContext dbContext, ILogger<PageMetaResolver> logger) : IPageMetaResolver
{
    public async Task<PageMetaOverrideDto?> ResolveAsync(string pageKey, string languageCode, CancellationToken cancellationToken)
    {
        try
        {
            var key = (pageKey ?? string.Empty).Trim().ToLowerInvariant();
            var row = await dbContext.PageMetaOverrides
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PageKey == key && p.LanguageCode == languageCode, cancellationToken);
            return row is null ? null : new PageMetaOverrideDto(row.PageKey, row.LanguageCode, row.Title, row.Description);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not read the page-meta override for {Page}/{Language}.", pageKey, languageCode);
            return null;
        }
    }
}
