using System.Security.Cryptography;
using GaiaSkyline.Application.Bookings;
using GaiaSkyline.Domain.Bookings;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Bookings;

/// <summary>
/// Data-protection-signed magic links with single-use + 30-minute expiry tracked in the database.
/// The signed value is just the row id, so the token is tamper-proof and reveals nothing.
/// </summary>
internal sealed class GuestMagicLinkService(
    AppDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider clock) : IGuestMagicLinkService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("GaiaSkyline.GuestMagicLink.v1");

    public async Task<string?> IssueAsync(string referenceCode, string email, CancellationToken cancellationToken)
    {
        var reference = referenceCode.Trim().ToUpperInvariant();
        var trimmedEmail = email.Trim();

        // Default SQL Server collation is case-insensitive, so the email compare ignores casing.
        var matches = await dbContext.Bookings
            .AsNoTracking()
            .AnyAsync(b => b.ReferenceCode == reference && b.GuestEmail == trimmedEmail, cancellationToken);
        if (!matches)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var link = new GuestMagicLink(Guid.NewGuid(), reference, now, now.Add(Lifetime));
        dbContext.GuestMagicLinks.Add(link);
        await dbContext.SaveChangesAsync(cancellationToken);

        return _protector.Protect(link.Id.ToString());
    }

    public async Task<string?> ConsumeAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        Guid id;
        try
        {
            id = Guid.Parse(_protector.Unprotect(token));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }

        var link = await dbContext.GuestMagicLinks.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (link is null || !link.IsUsable(now))
        {
            return null;
        }

        link.Consume(now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return link.BookingReference;
    }
}
