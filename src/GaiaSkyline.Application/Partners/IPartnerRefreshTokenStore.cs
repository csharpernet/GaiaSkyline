namespace GaiaSkyline.Application.Partners;

/// <summary>The raw refresh token (returned once) plus the owning user and its expiry.</summary>
public sealed record RefreshRotation(Guid UserId, string RawToken, DateTime ExpiresAtUtc);

/// <summary>
/// Issues and rotates partner refresh tokens. Tokens are stored hashed and rotate on every use; using a
/// token that was already rotated or revoked revokes the whole family (reuse detection).
/// </summary>
public interface IPartnerRefreshTokenStore
{
    Task<RefreshRotation> IssueAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Rotates a valid token; returns null (and revokes the family on reuse) otherwise.</summary>
    Task<RefreshRotation?> RotateAsync(string rawToken, CancellationToken cancellationToken);
}
