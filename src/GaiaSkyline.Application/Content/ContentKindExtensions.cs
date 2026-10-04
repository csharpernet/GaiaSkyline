using GaiaSkyline.Domain.Content;

namespace GaiaSkyline.Application.Content;

/// <summary>Presentation helpers over <see cref="ContentKind"/> shared by the admin editor and grid.</summary>
public static class ContentKindExtensions
{
    /// <summary>
    /// True for kinds whose value is free text authored per language (translated). Url, Number, Boolean and
    /// media refs are authored in English and fall back (ADR 0007), so a missing non-English value is not a
    /// gap. A Boolean block that carries a label (amenities) is text too — handled where the label is known.
    /// </summary>
    public static bool IsLocalizedText(this ContentKind kind) =>
        kind is ContentKind.PlainText or ContentKind.RichText or ContentKind.ShortText;

    /// <summary>True for the single HTML-bearing kind, which the admin edits with the rich-text editor.</summary>
    public static bool IsRichText(this ContentKind kind) => kind is ContentKind.RichText;
}
