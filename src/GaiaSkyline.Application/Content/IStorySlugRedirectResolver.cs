namespace GaiaSkyline.Application.Content;

/// <summary>
/// Resolves a previous story slug (kept after a published story was renamed) to the story's current slug, so
/// the public story page can 301 an old, possibly-indexed URL to the live one. Returns null when the slug is
/// not a known alias. Stage 7 §4 (the general redirects table arrives in §5).
/// </summary>
public interface IStorySlugRedirectResolver
{
    Task<string?> ResolveCurrentSlugAsync(string oldSlug, CancellationToken cancellationToken);
}
