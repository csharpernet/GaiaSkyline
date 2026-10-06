namespace GaiaSkyline.Application.Content;

/// <summary>
/// Resolves a story slug that no longer serves <paramref name="language"/> to the slug that does, so the
/// public story page can 301 an old — possibly indexed — URL to the live one. Covers previous slugs kept
/// after a rename (per-language or canonical) and another language's slug for the same story. Returns null
/// when nothing matches. Stage 7 §4 (the general redirects table arrived in §5).
/// </summary>
public interface IStorySlugRedirectResolver
{
    Task<string?> ResolveCurrentSlugAsync(string oldSlug, string language, CancellationToken cancellationToken);
}
