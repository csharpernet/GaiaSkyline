namespace GaiaSkyline.Domain.Identity;

/// <summary>
/// A hashed partner refresh token. Tokens belong to a family: using one rotates it (consume + issue a
/// successor in the same family). Presenting an already-consumed or revoked token is treated as theft,
/// and the whole family is revoked.
/// </summary>
public sealed class RefreshToken
{
    // Required by EF Core's materialization.
    private RefreshToken()
    {
    }

    public RefreshToken(Guid id, Guid userId, Guid familyId, string tokenHash, DateTime createdAtUtc, DateTime expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        Id = id;
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid FamilyId { get; private set; }

    /// <summary>SHA-256 of the raw token; the raw value is never stored.</summary>
    public string TokenHash { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? ConsumedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public bool IsActive(DateTime nowUtc) =>
        ConsumedAtUtc is null && RevokedAtUtc is null && nowUtc < ExpiresAtUtc;

    public bool WasUsedOrRevoked => ConsumedAtUtc is not null || RevokedAtUtc is not null;

    public void Consume(DateTime atUtc) => ConsumedAtUtc = atUtc;

    public void Revoke(DateTime atUtc) => RevokedAtUtc ??= atUtc;
}
