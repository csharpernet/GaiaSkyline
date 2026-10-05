namespace GaiaSkyline.Application.Seo;

/// <summary>Where a matched redirect should send the request.</summary>
public sealed record RedirectTarget(string ToPath, bool Permanent);

/// <summary>
/// Resolves a request path against the configured redirect rules, for the before-routing middleware. Backed by
/// an in-memory index so it costs nothing per request beyond a dictionary lookup; the index refreshes when the
/// content revision changes (adds/deletes bump it). Returns null when no rule matches. Stage 7 §5.
/// </summary>
public interface IRedirectResolver
{
    Task<RedirectTarget?> ResolveAsync(string path, CancellationToken cancellationToken);
}
