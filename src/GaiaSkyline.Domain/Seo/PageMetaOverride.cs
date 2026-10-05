using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Seo;

/// <summary>
/// An owner override of a page's SEO for one language: the &lt;title&gt; / meta description and the robots
/// directives (index/noindex, follow/nofollow). Keyed by the page's site-relative path (e.g. "", "gallery",
/// "legal/terms") and language; a blank title/description leaves the built-in default, and index+follow is the
/// default robots state. Stage 7 §5.
/// </summary>
public sealed class PageMetaOverride : Entity<PageMetaOverrideId>
{
    // Required by EF Core's materialization.
    private PageMetaOverride()
    {
    }

    public PageMetaOverride(
        PageMetaOverrideId id, string pageKey, string languageCode, string? title, string? description,
        bool noIndex, bool noFollow, DateTime updatedAtUtc, string updatedBy)
    {
        ArgumentNullException.ThrowIfNull(pageKey); // "" is a valid key (the home page)
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        Id = id;
        PageKey = pageKey.Trim().ToLowerInvariant();
        LanguageCode = languageCode.Trim();
        Set(title, description, noIndex, noFollow, updatedAtUtc, updatedBy);
    }

    /// <summary>Site-relative page path without a leading slash ("" = home, "gallery", "legal/terms").</summary>
    public string PageKey { get; private set; } = null!;

    public string LanguageCode { get; private set; } = null!;

    public string? Title { get; private set; }

    public string? Description { get; private set; }

    /// <summary>True emits robots noindex for this page+language.</summary>
    public bool NoIndex { get; private set; }

    /// <summary>True emits robots nofollow for this page+language.</summary>
    public bool NoFollow { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; } = null!;

    public void Set(string? title, string? description, bool noIndex, bool noFollow, DateTime updatedAtUtc, string updatedBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatedBy);
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        NoIndex = noIndex;
        NoFollow = noFollow;
        UpdatedAtUtc = updatedAtUtc;
        UpdatedBy = updatedBy.Trim();
    }

    /// <summary>True when the row carries nothing but defaults (no title, no description, index + follow) — it can be deleted.</summary>
    public bool IsEmpty => Title is null && Description is null && !NoIndex && !NoFollow;
}
