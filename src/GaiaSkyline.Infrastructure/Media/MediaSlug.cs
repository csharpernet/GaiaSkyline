using System.Globalization;
using System.Text;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// Turns a human title (usually the uploaded file name) into an SEO-friendly filename stem: lower-case ASCII,
/// diacritics folded, every run of other characters collapsed to a single hyphen, length-capped. Returns an
/// empty string when nothing usable remains (the caller then falls back to the GUID). Stage 7E-2d.
/// </summary>
internal static class MediaSlug
{
    private const int MaxLength = 120;

    public static string From(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var name = title.Trim();

        // Drop a trailing file extension if the title is a file name (e.g. "Douro Balcony.JPG").
        var dot = name.LastIndexOf('.');
        if (dot > 0 && name.Length - dot <= 6)
        {
            name = name[..dot];
        }

        // Fold diacritics: decompose, drop the combining marks, recompose.
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(ch);
            }
        }

        var lowered = folded.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        var slug = new StringBuilder(lowered.Length);
        var pendingHyphen = false;
        foreach (var ch in lowered)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                if (pendingHyphen && slug.Length > 0)
                {
                    slug.Append('-');
                }

                slug.Append(ch);
                pendingHyphen = false;
            }
            else
            {
                pendingHyphen = true;
            }
        }

        var result = slug.ToString();
        if (result.Length > MaxLength)
        {
            result = result[..MaxLength].TrimEnd('-');
        }

        return result;
    }
}
