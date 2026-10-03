using System.Security.Cryptography;
using System.Text;
using GaiaSkyline.Application.Partners;
using GaiaSkyline.Domain.Identity;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Partners;

/// <summary>
/// EF Core refresh-token store. Tokens are random 256-bit values stored as SHA-256 hashes; rotation
/// consumes the presented token and issues a successor in the same family. Presenting a consumed or
/// revoked token revokes every active token in that family.
/// </summary>
internal sealed class PartnerRefreshTokenStore(AppDbContext dbContext, TimeProvider clock) : IPartnerRefreshTokenStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public async Task<RefreshRotation> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var raw = GenerateRawToken();
        var token = new RefreshToken(Guid.NewGuid(), userId, Guid.NewGuid(), Hash(raw), now, now.Add(Lifetime));
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RefreshRotation(userId, raw, token.ExpiresAtUtc);
    }

    public async Task<RefreshRotation?> RotateAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var hash = Hash(rawToken);
        var token = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        if (token.WasUsedOrRevoked)
        {
            // Reuse of an already-rotated/revoked token → treat as theft; revoke the entire family.
            var family = await dbContext.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var member in family)
            {
                member.Revoke(now);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        if (now >= token.ExpiresAtUtc)
        {
            return null;
        }

        token.Consume(now);
        var raw = GenerateRawToken();
        var successor = new RefreshToken(Guid.NewGuid(), token.UserId, token.FamilyId, Hash(raw), now, now.Add(Lifetime));
        dbContext.RefreshTokens.Add(successor);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new RefreshRotation(token.UserId, raw, successor.ExpiresAtUtc);
    }

    private static string GenerateRawToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
