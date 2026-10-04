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

    public async Task<bool> SetTranslationsAsync(
        string key, IReadOnlyCollection<ContentTranslationEdit> edits, string actor, CancellationToken cancellationToken)
    {
        var block = await dbContext.ContentBlocks
            .Include(b => b.Translations)
            .FirstOrDefaultAsync(b => b.Key == key, cancellationToken);
        if (block is null)
        {
            return false;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var staged = false;
        foreach (var edit in edits)
        {
            var text = block.Kind == ContentKind.RichText ? sanitizer.Sanitize(edit.Value.Text) : edit.Value.Text;
            MediaAssetId? mediaId = edit.Value.MediaAssetId is { } id ? MediaAssetId.From(id) : null;

            // Only stage a draft where the owner actually changed something, so untouched languages are
            // not marked "pending" and don't clutter the draft/publish indicators.
            if (!HasChanged(block, edit.Language, text, edit.Value.Number, edit.Value.Boolean, mediaId))
            {
                continue;
            }

            block.SetDraftTranslation(edit.Language, text, mediaId, edit.Value.Number, edit.Value.Boolean, now, actor);
            staged = true;
        }

        // Staged drafts never touch the public site, so the output/content caches are left intact.
        if (staged)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static bool HasChanged(
        ContentBlock block, string language, string? text, decimal? number, bool? boolean, MediaAssetId? mediaId)
    {
        var current = block.Translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase));
        if (current is null)
        {
            // No row yet: a non-empty submission is a change; an all-empty one is not.
            return !string.IsNullOrWhiteSpace(text) || number.HasValue || boolean.HasValue || mediaId.HasValue;
        }

        return block.Kind switch
        {
            ContentKind.Number => number != current.ValueNumber,
            ContentKind.Boolean => boolean != current.ValueBoolean
                || !string.Equals(text ?? string.Empty, current.ValueText ?? string.Empty, StringComparison.Ordinal),
            ContentKind.ImageRef or ContentKind.VideoRef => mediaId != current.ValueMediaAssetId,
            _ => !string.Equals(text ?? string.Empty, current.ValueText ?? string.Empty, StringComparison.Ordinal),
        };
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
