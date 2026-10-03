namespace GaiaSkyline.Application.Bookings;

/// <summary>
/// Issues and consumes single-use, 30-minute magic links that grant a guest access to one booking
/// without an account. Issuing is enumeration-safe: the caller gets no signal about whether the booking
/// exists (this returns null silently on a mismatch).
/// </summary>
public interface IGuestMagicLinkService
{
    /// <summary>A signed token when the reference + email match a booking; otherwise null.</summary>
    Task<string?> IssueAsync(string referenceCode, string email, CancellationToken cancellationToken);

    /// <summary>The booking reference if the token is valid, unexpired and unused (and marks it used); else null.</summary>
    Task<string?> ConsumeAsync(string token, CancellationToken cancellationToken);
}
