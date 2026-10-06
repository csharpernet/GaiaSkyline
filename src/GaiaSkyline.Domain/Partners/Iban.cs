namespace GaiaSkyline.Domain.Partners;

/// <summary>
/// IBAN checksum validation (ISO 13616 mod-97) for partner payout details — Stage 8 Part A. Validates the
/// shape and check digits only; it does not verify the account exists.
/// </summary>
public static class Iban
{
    /// <summary>
    /// True when <paramref name="input"/> is a structurally valid IBAN; <paramref name="normalized"/> is the
    /// canonical form (uppercase, no spaces) when valid, null otherwise.
    /// </summary>
    public static bool IsValid(string? input, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var iban = input.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        if (iban.Length is < 15 or > 34
            || !char.IsAsciiLetterUpper(iban[0]) || !char.IsAsciiLetterUpper(iban[1])
            || !char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3])
            || !iban.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        // Move the country code + check digits to the end, map letters to 10..35, and take mod 97
        // incrementally so the number never overflows.
        var rearranged = string.Concat(iban.AsSpan(4), iban.AsSpan(0, 4));
        var remainder = 0;
        foreach (var c in rearranged)
        {
            if (char.IsAsciiDigit(c))
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else
            {
                var value = c - 'A' + 10;
                remainder = (remainder * 100 + value) % 97;
            }
        }

        if (remainder != 1)
        {
            return false;
        }

        normalized = iban;
        return true;
    }
}
