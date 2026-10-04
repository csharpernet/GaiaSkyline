using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>
/// EF Core reads for the admin content editor: every block (published or not) with its draft and published
/// values per language, plus the media library for the pickers. Reads are untracked.
/// </summary>
internal sealed class AdminContentReadService(AppDbContext dbContext) : IAdminContentReadService
{
    // Friendly section headings; unknown sections fall back to a title-cased key.
    private static readonly Dictionary<string, string> SectionNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["home"] = "Home",
        ["rules"] = "House rules",
        ["faq"] = "FAQ",
        ["footer"] = "Footer",
        ["amenities"] = "Amenities",
        ["checkin"] = "Check-in instructions",
        ["emails"] = "Emails",
        ["legal"] = "Legal",
        ["partners"] = "Partners",
    };

    public async Task<AdminContentOverview> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var blocks = await dbContext.ContentBlocks
            .AsNoTracking()
            .Include(b => b.Translations)
            .OrderBy(b => b.Section)
            .ThenBy(b => b.DisplayOrder)
            .ThenBy(b => b.Key)
            .ToListAsync(cancellationToken);

        var sections = blocks
            .GroupBy(b => b.Section, StringComparer.Ordinal)
            .Select(g => new AdminContentSection(
                g.Key,
                SectionDisplayName(g.Key),
                g.Select(ToSummary).ToList()))
            .ToList();

        return new AdminContentOverview(sections);
    }

    public async Task<AdminContentBlockDetail?> GetBlockAsync(string key, CancellationToken cancellationToken)
    {
        var block = await dbContext.ContentBlocks
            .AsNoTracking()
            .Include(b => b.Translations)
            .FirstOrDefaultAsync(b => b.Key == key, cancellationToken);
        if (block is null)
        {
            return null;
        }

        var languages = ContentLanguages.All
            .Select(lang =>
            {
                var t = Find(block, lang);
                return new AdminContentBlockLanguage(
                    lang,
                    t?.ValueText,
                    t?.ValueNumber,
                    t?.ValueBoolean,
                    t?.ValueMediaAssetId?.Value,
                    t?.HasDraft ?? false,
                    t?.DraftText,
                    t?.DraftNumber,
                    t?.DraftBoolean,
                    t?.DraftMediaAssetId?.Value);
            })
            .ToList();

        return new AdminContentBlockDetail(
            block.Key, block.Section, block.Kind, block.DisplayName,
            block.IsPublished, block.HasPendingChanges, block.UpdatedAtUtc, block.UpdatedBy, languages);
    }

    public async Task<IReadOnlyList<MediaAssetDto>> GetAssetsAsync(MediaKind? kind, CancellationToken cancellationToken)
    {
        var query = dbContext.MediaAssets.AsNoTracking().Where(a => !a.IsDeleted);
        if (kind is { } k)
        {
            query = query.Where(a => a.Kind == k);
        }

        var assets = await query
            .OrderByDescending(a => a.UploadedAtUtc)
            .ToListAsync(cancellationToken);

        return assets
            .Select(a => new MediaAssetDto(
                a.Id.Value, a.Kind, a.BlobUri, a.PosterBlobUri, a.Width, a.Height,
                a.DurationSec, a.ByteSize, a.ContentType, a.AltText, a.Lqip))
            .ToList();
    }

    private static AdminContentBlockSummary ToSummary(ContentBlock block)
    {
        // A Boolean block that carries an English label (amenities) is localized text too; otherwise the
        // kind alone decides. Url/Number/media refs are English-authoritative and never flagged as gaps.
        var englishLabel = Find(block, ContentLanguages.Default)?.ValueText;
        var expectsText = block.Kind.IsLocalizedText()
            || (block.Kind == ContentKind.Boolean && !string.IsNullOrWhiteSpace(englishLabel));

        var languages = ContentLanguages.All
            .Select(lang =>
            {
                var t = Find(block, lang);
                var hasPublished = HasPublishedValue(block.Kind, t);
                var isPlaceholder = ContentPlaceholders.IsPlaceholder(t?.ValueText);
                var isEmpty = expectsText && string.IsNullOrWhiteSpace(t?.ValueText);
                return new AdminContentLanguageStatus(lang, hasPublished, t?.HasDraft ?? false, isPlaceholder, isEmpty);
            })
            .ToList();

        return new AdminContentBlockSummary(
            block.Key, block.Section, block.Kind, block.DisplayName, block.DisplayOrder,
            block.IsPublished, block.HasPendingChanges, block.UpdatedAtUtc, block.UpdatedBy, languages);
    }

    private static bool HasPublishedValue(ContentKind kind, ContentTranslation? translation) => kind switch
    {
        _ when translation is null => false,
        ContentKind.Number => translation.ValueNumber.HasValue,
        ContentKind.Boolean => translation.ValueBoolean.HasValue,
        ContentKind.ImageRef or ContentKind.VideoRef => translation.ValueMediaAssetId.HasValue,
        _ => !string.IsNullOrWhiteSpace(translation.ValueText),
    };

    private static ContentTranslation? Find(ContentBlock block, string language) =>
        block.Translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase));

    private static string SectionDisplayName(string section) =>
        SectionNames.TryGetValue(section, out var name)
            ? name
            : string.Concat(section[..1].ToUpperInvariant(), section[1..]);
}
