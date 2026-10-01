using System.Security.Cryptography;
using System.Text;

namespace GaiaSkyline.Infrastructure.Data;

/// <summary>
/// Produces stable GUIDs from a logical name so seed data has deterministic identities. This is
/// what makes the seeder idempotent: re-running computes the same ids and skips existing rows.
/// </summary>
internal static class DeterministicGuid
{
    public static Guid From(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name));
        return new Guid(hash.AsSpan(0, 16));
    }
}
