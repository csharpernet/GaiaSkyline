using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Content;

/// <summary>
/// A single language's value for a <see cref="ContentBlock"/>. Exactly which value column is
/// meaningful is governed by the owning block's <see cref="ContentBlock.Kind"/>.
/// </summary>
public sealed class ContentTranslation : Entity<ContentTranslationId>
{
    // Required by EF Core's materialization.
    private ContentTranslation()
    {
    }

    public ContentTranslation(
        ContentTranslationId id,
        ContentBlockId contentBlockId,
        string languageCode,
        string? valueText,
        MediaAssetId? valueMediaAssetId,
        decimal? valueNumber,
        bool? valueBoolean,
        DateTime updatedAtUtc,
        string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Id = id;
        ContentBlockId = contentBlockId;
        LanguageCode = languageCode.Trim();
        ValueText = valueText;
        ValueMediaAssetId = valueMediaAssetId;
        ValueNumber = valueNumber;
        ValueBoolean = valueBoolean;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy;
    }

    public ContentBlockId ContentBlockId { get; private set; }

    /// <summary>BCP-47 tag, stored as-is (e.g. "en", "pt-PT").</summary>
    public string LanguageCode { get; private set; } = null!;

    public string? ValueText { get; private set; }

    public MediaAssetId? ValueMediaAssetId { get; private set; }

    public decimal? ValueNumber { get; private set; }

    public bool? ValueBoolean { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; } = null!;

    // --- Staged draft (Stage 7D): the owner edits a draft that stays invisible to the public until
    //     published. HasDraft true means the Draft* values override the Value* values in preview. ---

    public bool HasDraft { get; private set; }

    public string? DraftText { get; private set; }

    public MediaAssetId? DraftMediaAssetId { get; private set; }

    public decimal? DraftNumber { get; private set; }

    public bool? DraftBoolean { get; private set; }

    /// <summary>The value preview should show: the draft when one is pending, otherwise the published value.</summary>
    public string? EffectiveText => HasDraft ? DraftText : ValueText;

    public MediaAssetId? EffectiveMediaAssetId => HasDraft ? DraftMediaAssetId : ValueMediaAssetId;

    public decimal? EffectiveNumber => HasDraft ? DraftNumber : ValueNumber;

    public bool? EffectiveBoolean => HasDraft ? DraftBoolean : ValueBoolean;

    internal void Update(
        string? valueText,
        MediaAssetId? valueMediaAssetId,
        decimal? valueNumber,
        bool? valueBoolean,
        DateTime updatedAtUtc,
        string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        ValueText = valueText;
        ValueMediaAssetId = valueMediaAssetId;
        ValueNumber = valueNumber;
        ValueBoolean = valueBoolean;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy;
    }

    /// <summary>Stages a draft edit (does not touch the published value).</summary>
    internal void SetDraft(
        string? valueText,
        MediaAssetId? valueMediaAssetId,
        decimal? valueNumber,
        bool? valueBoolean,
        DateTime updatedAtUtc,
        string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        HasDraft = true;
        DraftText = valueText;
        DraftMediaAssetId = valueMediaAssetId;
        DraftNumber = valueNumber;
        DraftBoolean = valueBoolean;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy;
    }

    /// <summary>Promotes a pending draft to the published value (no-op when there is no draft).</summary>
    internal void PublishDraft(DateTime updatedAtUtc, string updatedBy)
    {
        if (!HasDraft)
        {
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        ValueText = DraftText;
        ValueMediaAssetId = DraftMediaAssetId;
        ValueNumber = DraftNumber;
        ValueBoolean = DraftBoolean;
        HasDraft = false;
        DraftText = null;
        DraftMediaAssetId = null;
        DraftNumber = null;
        DraftBoolean = null;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy;
    }
}
