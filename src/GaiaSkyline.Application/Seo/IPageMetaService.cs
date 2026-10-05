namespace GaiaSkyline.Application.Seo;

/// <summary>An owner override of a page's title/description for one language (blank = use the built-in default).</summary>
public sealed record PageMetaOverrideDto(string PageKey, string LanguageCode, string? Title, string? Description);

/// <summary>
/// Resolves a page's SEO meta override for a language (exact language, no fallback — a missing override leaves
/// the page's built-in default). Used by the public pages. Resilient: returns null if it cannot read. Stage 7 §5.
/// </summary>
public interface IPageMetaResolver
{
    Task<PageMetaOverrideDto?> ResolveAsync(string pageKey, string languageCode, CancellationToken cancellationToken);
}

/// <summary>Owner management of per-page, per-language meta overrides. Stage 7 §5.</summary>
public interface IPageMetaAdminService
{
    /// <summary>All currently-set overrides (empty rows are not returned).</summary>
    Task<IReadOnlyList<PageMetaOverrideDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>Set (or clear, when both fields are blank) the override for a known page + language. False if the page is unknown.</summary>
    Task<bool> UpsertAsync(string pageKey, string languageCode, string? title, string? description, string actor, CancellationToken cancellationToken);
}
