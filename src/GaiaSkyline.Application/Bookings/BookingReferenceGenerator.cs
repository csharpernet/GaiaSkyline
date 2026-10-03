using System.Security.Cryptography;

namespace GaiaSkyline.Application.Bookings;

/// <summary>
/// Generates codes like "GS-8K3M" from an unambiguous alphabet (no I/O/0/1). Uniqueness is enforced
/// by the database's unique index; the creation service retries on the rare collision.
/// </summary>
public sealed class BookingReferenceGenerator : IBookingReferenceGenerator
{
    private const string Prefix = "GS-";
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // 31 chars, no I L O 0 1
    private const int Length = 4;

    public string Next()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return Prefix + new string(chars);
    }
}
