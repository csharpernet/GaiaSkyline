using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Content;

/// <summary>
/// A single editable piece of site content, addressed by a stable dotted <see cref="Key"/>
/// (e.g. "home.hero.headline"). Carries per-language values as <see cref="ContentTranslation"/>s.
/// The public site reads only blocks where <see cref="IsPublished"/> is true.
/// </summary>
public sealed class ContentBlock : Entity<ContentBlockId>
{
    private readonly List<ContentTranslation> _translations = [];

    // Required by EF Core's materialization.
    private ContentBlock()
    {
    }

    public ContentBlock(
        ContentBlockId id,
        string key,
        ContentKind kind,
        string section,
        string displayName,
        int displayOrder,
        bool isPublished,
        DateTime updatedAtUtc,
        string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Id = id;
        Key = key.Trim();
        Kind = kind;
        Section = section.Trim();
        DisplayName = displayName.Trim();
        DisplayOrder = displayOrder;
        IsPublished = isPublished;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy;
    }

    /// <summary>Stable dotted address, unique across all blocks (e.g. "home.hero.headline").</summary>
    public string Key { get; private set; } = null!;

    public ContentKind Kind { get; private set; }

    /// <summary>Grouping used by the read API and admin (e.g. "home", "footer").</summary>
    public string Section { get; private set; } = null!;

    /// <summary>Admin-facing label.</summary>
    public string DisplayName { get; private set; } = null!;

    public int DisplayOrder { get; private set; }

    public bool IsPublished { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; } = null!;

    public IReadOnlyCollection<ContentTranslation> Translations => _translations.AsReadOnly();

    /// <summary>
    /// Insert or update the value for a language (upsert keyed by <paramref name="languageCode"/>).
    /// </summary>
    public ContentTranslation SetTranslation(
        string languageCode,
        string? valueText,
        MediaAssetId? valueMediaAssetId,
        decimal? valueNumber,
        bool? valueBoolean,
        DateTime updatedAtUtc,
        string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        var normalized = languageCode.Trim();

        var existing = _translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Update(valueText, valueMediaAssetId, valueNumber, valueBoolean, updatedAtUtc, updatedBy);
            return existing;
        }

        var translation = new ContentTranslation(
            ContentTranslationId.New(),
            Id,
            normalized,
            valueText,
            valueMediaAssetId,
            valueNumber,
            valueBoolean,
            updatedAtUtc,
            updatedBy);
        _translations.Add(translation);
        return translation;
    }

    /// <summary>
    /// Stage a draft edit for a language (upsert). The published value is untouched until
    /// <see cref="PublishDrafts"/>, so the public site keeps showing the live value meanwhile.
    /// </summary>
    public void SetDraftTranslation(
        string languageCode,
        string? valueText,
        MediaAssetId? valueMediaAssetId,
        decimal? valueNumber,
        bool? valueBoolean,
        DateTime updatedAtUtc,
        string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        var normalized = languageCode.Trim();

        var existing = _translations.FirstOrDefault(
            t => string.Equals(t.LanguageCode, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new ContentTranslation(
                ContentTranslationId.New(), Id, normalized, null, null, null, null, updatedAtUtc, updatedBy);
            _translations.Add(existing);
        }

        existing.SetDraft(valueText, valueMediaAssetId, valueNumber, valueBoolean, updatedAtUtc, updatedBy);
    }

    /// <summary>Promotes all pending draft translations to their published values and publishes the block.</summary>
    public void PublishDrafts(DateTime updatedAtUtc, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
        foreach (var translation in _translations)
        {
            translation.PublishDraft(updatedAtUtc, updatedBy);
        }

        IsPublished = true;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy;
    }

    /// <summary>True when the block is unpublished or any language has an unpublished draft edit.</summary>
    public bool HasPendingChanges => !IsPublished || _translations.Any(t => t.HasDraft);

    /// <summary>Flip the draft/publish switch.</summary>
    public void SetPublished(bool isPublished, DateTime updatedAtUtc, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
        IsPublished = isPublished;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy;
    }
}
