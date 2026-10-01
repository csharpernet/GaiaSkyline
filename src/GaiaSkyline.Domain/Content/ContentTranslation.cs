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
}
