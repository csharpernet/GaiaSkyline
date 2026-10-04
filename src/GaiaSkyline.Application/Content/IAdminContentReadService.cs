using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Media;

namespace GaiaSkyline.Application.Content;

/// <summary>One language's status for a block, for the editor tabs and the translation grid.</summary>
public sealed record AdminContentLanguageStatus(
    string LanguageCode,
    bool HasPublishedValue,
    bool HasDraft,
    bool IsPlaceholder,
    bool IsEmpty);

/// <summary>A block with its per-language status — a row in the section list and the translation grid.</summary>
public sealed record AdminContentBlockSummary(
    string Key,
    string Section,
    ContentKind Kind,
    string DisplayName,
    int DisplayOrder,
    bool IsPublished,
    bool HasPendingChanges,
    DateTime UpdatedAtUtc,
    string UpdatedBy,
    IReadOnlyList<AdminContentLanguageStatus> Languages);

/// <summary>A content section (e.g. "home") with a friendly display name and its blocks.</summary>
public sealed record AdminContentSection(
    string Key,
    string DisplayName,
    IReadOnlyList<AdminContentBlockSummary> Blocks);

/// <summary>Everything the section list and the translation grid need, in one read.</summary>
public sealed record AdminContentOverview(IReadOnlyList<AdminContentSection> Sections);

/// <summary>One language's published and draft values for the block editor.</summary>
public sealed record AdminContentBlockLanguage(
    string LanguageCode,
    string? Text,
    decimal? Number,
    bool? Boolean,
    Guid? MediaAssetId,
    bool HasDraft,
    string? DraftText,
    decimal? DraftNumber,
    bool? DraftBoolean,
    Guid? DraftMediaAssetId);

/// <summary>A single block with all five languages' values, for the editor.</summary>
public sealed record AdminContentBlockDetail(
    string Key,
    string Section,
    ContentKind Kind,
    string DisplayName,
    bool IsPublished,
    bool HasPendingChanges,
    DateTime UpdatedAtUtc,
    string UpdatedBy,
    IReadOnlyList<AdminContentBlockLanguage> Languages);

/// <summary>
/// Owner-only content reads for the admin editor: every block (published or not) with its draft and
/// published values per language, plus the media library for the image/video pickers. Separate from the
/// public <see cref="IContentReadStore"/>, which returns only what the live site needs.
/// </summary>
public interface IAdminContentReadService
{
    /// <summary>All sections and blocks with per-language status — feeds the section list and translation grid.</summary>
    Task<AdminContentOverview> GetOverviewAsync(CancellationToken cancellationToken);

    /// <summary>One block with every language's published and draft values; null when the key is unknown.</summary>
    Task<AdminContentBlockDetail?> GetBlockAsync(string key, CancellationToken cancellationToken);

    /// <summary>Media assets for the picker, newest first, optionally filtered to one kind.</summary>
    Task<IReadOnlyList<MediaAssetDto>> GetAssetsAsync(MediaKind? kind, CancellationToken cancellationToken);
}
