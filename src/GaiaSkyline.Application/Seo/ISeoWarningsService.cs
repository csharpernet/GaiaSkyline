namespace GaiaSkyline.Application.Seo;

/// <summary>One SEO/accessibility issue surfaced on the owner's SEO dashboard. Stage 7 §5.</summary>
public sealed record SeoWarning(string Category, string Message, string? Link);

/// <summary>
/// Aggregates the SEO/accessibility issues worth the owner's attention: images missing alt text in a content
/// language, and published stories missing a translation or a meta description. Stage 7 §5.
/// </summary>
public interface ISeoWarningsService
{
    Task<IReadOnlyList<SeoWarning>> GetWarningsAsync(CancellationToken cancellationToken);
}
