using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using GaiaSkyline.Application.Security;
using Microsoft.Extensions.Logging;

namespace GaiaSkyline.Infrastructure.Security;

/// <summary>
/// Have I Been Pwned range API client (k-anonymity): only the first five SHA-1 hex characters are sent.
/// Fails open — a network error logs a warning and returns <c>false</c>.
/// </summary>
internal sealed class PwnedPasswordsClient(HttpClient httpClient, ILogger<PwnedPasswordsClient> logger)
    : IPwnedPasswordsClient
{
    public async Task<bool> IsPwnedAsync(string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var hash = Sha1Hex(password);
        var prefix = hash[..5];
        var suffix = hash[5..];

        try
        {
            using var response = await httpClient.GetAsync(new Uri($"range/{prefix}", UriKind.Relative), cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = line.IndexOf(':', StringComparison.Ordinal);
                if (separator > 0 && string.Equals(line[..separator], suffix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "HIBP range query failed; failing open for the breach check.");
            return false;
        }
    }

    // HIBP's range API is defined over SHA-1 of the password; this is not a security primitive here,
    // only the lookup key for the k-anonymity protocol.
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "SHA-1 is required by the HIBP range protocol; used as a lookup key, not for security.")]
    [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms",
        Justification = "SHA-1 is required by the HIBP range protocol; used as a lookup key, not for security.")]
    private static string Sha1Hex(string password)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}
