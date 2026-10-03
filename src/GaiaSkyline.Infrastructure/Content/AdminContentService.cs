using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>
/// EF Core content writes for the Owner. Each successful change bumps <see cref="IContentRevision"/>,
/// which is part of the output- and content-cache keys, so the public site reflects edits immediately
/// after publish.
/// </summary>
internal sealed class AdminContentService(
    AppDbContext dbContext,
    IContentRevision revision,
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

        MediaAssetId? mediaId = value.MediaAssetId is { } id ? MediaAssetId.From(id) : null;
        block.SetTranslation(language, value.Text, mediaId, value.Number, value.Boolean,
            clock.GetUtcNow().UtcDateTime, actor);

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
