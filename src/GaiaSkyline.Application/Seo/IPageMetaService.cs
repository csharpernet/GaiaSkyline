namespace GaiaSkyline.Application.Seo;

/// <summary>
/// An owner override of a page's SEO for one language: title/description (blank = use the built-in default) and
/// the robots directives (<see cref="NoIndex"/> / <see cref="NoFollow"/>; default is index + follow).
/// </summary>
public sealed record PageMetaOverrideDto(
    string PageKey, string LanguageCode, string? Title, string? Description, bool NoIndex, bool NoFollow);

/// <summary>
/// Resolves a page's SEO override for a language (exact language, no fallback — a missing override leaves the
/// page's built-in default). Used by the public pages. Resilient: returns null if it cannot read. Stage 7 §5.
/// </summary>
public interface IPageMetaResolver
{
    Task<PageMetaOverrideDto?> ResolveAsync(string pageKey, string languageCode, CancellationToken cancellationToken);

    /// <summary>
    /// The set of <c>"{pageKey}|{languageCode}"</c> overrides marked noindex — used to drop those URLs from
    /// the sitemap and flag them in the admin preview. Resilient: returns an empty set if it cannot read.
    /// </summary>
    Task<IReadOnlySet<string>> GetNoIndexKeysAsync(CancellationToken cancellationToken);
}

/// <summary>Owner management of per-page, per-language meta + robots overrides. Stage 7 §5.</summary>
public interface IPageMetaAdminService
{
    /// <summary>All currently-set overrides (rows that are nothing but defaults are not returned).</summary>
    Task<IReadOnlyList<PageMetaOverrideDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Set (or clear, when only defaults are left) the override for a known page + language. False if the page
    /// or language is unknown.
    /// </summary>
    Task<bool> UpsertAsync(
        string pageKey, string languageCode, string? title, string? description, bool noIndex, bool noFollow,
        string actor, CancellationToken cancellationToken);
}
