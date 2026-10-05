using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Seo;

/// <summary>
/// A URL redirect rule applied by middleware before routing: a request to <see cref="FromPath"/> is sent to
/// <see cref="ToPath"/> with a 301 (when <see cref="IsPermanent"/>) or 302. Paths are site-relative and
/// normalised (leading slash, no trailing slash); the from-path is matched case-insensitively (stored lower).
/// Loop detection lives in the admin service (it needs the whole set). Stage 7 §5.
/// </summary>
public sealed class Redirect : Entity<RedirectId>
{
    // Required by EF Core's materialization.
    private Redirect()
    {
    }

    public Redirect(RedirectId id, string fromPath, string toPath, bool isPermanent, DateTime createdAtUtc, string createdBy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        var from = NormalizeFrom(fromPath);
        var to = NormalizeTo(toPath);
        if (from is null)
        {
            throw new ArgumentException("The from-path must be a site-relative path starting with '/'.", nameof(fromPath));
        }

        if (to is null)
        {
            throw new ArgumentException("The to-path must be a site-relative path starting with '/'.", nameof(toPath));
        }

        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A redirect cannot point a path at itself.", nameof(toPath));
        }

        Id = id;
        FromPath = from;
        ToPath = to;
        IsPermanent = isPermanent;
        CreatedAtUtc = createdAtUtc;
        CreatedBy = createdBy.Trim();
    }

    /// <summary>The requested path to match (lower-cased, leading slash, no trailing slash).</summary>
    public string FromPath { get; private set; } = null!;

    /// <summary>The destination path (leading slash, no trailing slash; case preserved).</summary>
    public string ToPath { get; private set; } = null!;

    public bool IsPermanent { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    /// <summary>Normalise a path for from-matching: trim, require a leading slash, drop a trailing slash, lower-case. Null if invalid.</summary>
    public static string? NormalizeFrom(string? path) => Normalize(path, toLower: true);

    /// <summary>Normalise a destination path: trim, require a leading slash, drop a trailing slash (case preserved). Null if invalid.</summary>
    public static string? NormalizeTo(string? path) => Normalize(path, toLower: false);

    private static string? Normalize(string? path, bool toLower)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim();
        if (toLower)
        {
            trimmed = trimmed.ToLowerInvariant();
        }

        if (!trimmed.StartsWith('/'))
        {
            return null;
        }

        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }
}
