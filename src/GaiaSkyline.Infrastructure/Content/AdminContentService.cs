using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>
/// EF Core content writes for the Owner. Edits are staged as drafts (invisible to the public, no cache
/// bump) and promoted on publish, which bumps <see cref="IContentRevision"/> so the public site reflects
/// them immediately. RichText is sanitized against an allowlist before it is stored.
/// </summary>
internal sealed class AdminContentService(
    AppDbContext dbContext,
    IContentRevision revision,
    IHtmlContentSanitizer sanitizer,
    TimeProvider clock) : IAdminContentService
{
    public async Task<bool> SetTranslationAsync(
        string key, string language, ContentValueDto value, string actor, CancellationToken cancellationToken)
    {
        var block = await dbContext.ContentBlocks
            .Include(b => b.Translations)
            .FirstOrDefaultAsync(b => b.Key == key, cancellationToken);
        if (block is null)
        {
            return false;
        }

        // RichText is the only HTML-bearing kind; sanitize it before it is ever stored.
        var text = block.Kind == ContentKind.RichText ? sanitizer.Sanitize(value.Text) : value.Text;
        MediaAssetId? mediaId = value.MediaAssetId is { } id ? MediaAssetId.From(id) : null;

        block.SetDraftTranslation(language, text, mediaId, value.Number, value.Boolean,
            clock.GetUtcNow().UtcDateTime, actor);

        // A staged draft does not change the public site, so the output/content caches are left intact.
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> PublishAsync(string key, string actor, CancellationToken cancellationToken)
    {
        var block = await dbContext.ContentBlocks
            .Include(b => b.Translations)
            .FirstOrDefaultAsync(b => b.Key == key, cancellationToken);
        if (block is null)
        {
            return false;
        }

        block.PublishDrafts(clock.GetUtcNow().UtcDateTime, actor);
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }

    public async Task<bool> SetPublishedAsync(string key, bool published, string actor, CancellationToken cancellationToken)
    {
        var block = await dbContext.ContentBlocks.FirstOrDefaultAsync(b => b.Key == key, cancellationToken);
        if (block is null)
        {
            return false;
        }

        block.SetPublished(published, clock.GetUtcNow().UtcDateTime, actor);
        await dbContext.SaveChangesAsync(cancellationToken);
        revision.Bump();
        return true;
    }
}
