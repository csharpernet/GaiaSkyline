namespace GaiaSkyline.Application.Seo;

/// <summary>A redirect rule for the admin list.</summary>
public sealed record RedirectDto(Guid Id, string FromPath, string ToPath, bool Permanent, DateTime CreatedAtUtc);

/// <summary>Outcome of adding a redirect (rejected on a bad path, a duplicate from-path or a loop).</summary>
public sealed record RedirectAddResult(bool Ok, Guid? Id, string? Error)
{
    public static RedirectAddResult Success(Guid id) => new(true, id, null);

    public static RedirectAddResult Fail(string error) => new(false, null, error);
}

/// <summary>
/// Owner-only management of redirect rules: list, add (normalised, deduped, with loop detection on save) and
/// delete. Each change bumps the content revision so the resolver's index refreshes. Stage 7 §5.
/// </summary>
public interface IRedirectAdminService
{
    Task<IReadOnlyList<RedirectDto>> GetAllAsync(CancellationToken cancellationToken);

    Task<RedirectAddResult> AddAsync(string fromPath, string toPath, bool permanent, string actor, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, string actor, CancellationToken cancellationToken);
}
