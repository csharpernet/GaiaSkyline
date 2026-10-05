using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Seo;

/// <summary>
/// An owner override of a page's SEO &lt;title&gt; / meta description for one language. Keyed by the page's
/// site-relative path (e.g. "", "gallery", "legal/terms") and language; a blank field leaves the page's
/// built-in default in place. Stage 7 §5.
/// </summary>
public sealed class PageMetaOverride : Entity<PageMetaOverrideId>
{
    // Required by EF Core's materialization.
    private PageMetaOverride()
    {
    }

    public PageMetaOverride(
        PageMetaOverrideId id, string pageKey, string languageCode, string? title, string? description,
        DateTime updatedAtUtc, string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(pageKey); // "" is a valid key (the home page)
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);

        Id = id;
        PageKey = pageKey.Trim().ToLowerInvariant();
        LanguageCode = languageCode.Trim();
        Set(title, description, updatedAtUtc, updatedBy);
    }

    /// <summary>Site-relative page path without a leading slash ("" = home, "gallery", "legal/terms").</summary>
    public string PageKey { get; private set; } = null!;

    public string LanguageCode { get; private set; } = null!;

    public string? Title { get; private set; }

    public string? Description { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; } = null!;

    public void Set(string? title, string? description, DateTime updatedAtUtc, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy.Trim();
    }

    /// <summary>True when neither a title nor a description is set (the row can be deleted).</summary>
    public bool IsEmpty => Title is null && Description is null;
}
