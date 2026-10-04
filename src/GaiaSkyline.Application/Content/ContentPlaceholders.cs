namespace GaiaSkyline.Application.Content;

/// <summary>
/// The leftover translation-placeholder prefixes (ADR 0007): the seeder writes each non-English text value
/// as the English value prefixed with "[PT] ", "[ES] ", "[FR] " or "[DE] ". The admin translation grid
/// flags any value that still carries one so the owner can find copy that was never actually translated.
/// </summary>
public static class ContentPlaceholders
{
    public static readonly IReadOnlyList<string> Prefixes = ["[PT]", "[ES]", "[FR]", "[DE]"];

    /// <summary>True when the text still starts with one of the seeded placeholder prefixes.</summary>
    public static bool IsPlaceholder(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.TrimStart();
        return Prefixes.Any(p => trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }
}
