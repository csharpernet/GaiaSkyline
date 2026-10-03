namespace GaiaSkyline.Application.Security;

/// <summary>
/// Checks a password against the Have I Been Pwned breach corpus using the k-anonymity range API
/// (only the first five SHA-1 hex characters ever leave the process). Implementations must
/// <b>fail open</b>: if the service is unreachable they return <c>false</c> and log a warning, so an
/// HIBP outage never blocks registration or a password change.
/// </summary>
public interface IPwnedPasswordsClient
{
    Task<bool> IsPwnedAsync(string password, CancellationToken cancellationToken);
}
