using System.Globalization;

namespace GaiaSkyline.Web.Admin;

/// <summary>
/// Tolerant money-input parsing for admin forms ("150", "150.50", "150,50", "1 234,56", "€95").
/// Admin requests carry the browser's Accept-Language culture, so a fixed-culture model bind would
/// reject half the inputs; forms bind money as strings and parse here instead.
/// </summary>
public static class AdminMoney
{
    /// <summary>Parses <paramref name="text"/> into a non-negative amount; blank parses to null and returns true.</summary>
    public static bool TryParse(string? text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var s = text.Replace(" ", "", StringComparison.Ordinal).Replace("€", "", StringComparison.Ordinal);
        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            // Both present: the later one is the decimal separator, the other is thousands.
            var (dec, thou) = lastComma > lastDot ? (',', '.') : ('.', ',');
            s = s.Replace(thou.ToString(), "", StringComparison.Ordinal).Replace(dec, '.');
        }
        else if (lastComma >= 0)
        {
            s = s.Replace(',', '.');
        }

        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            return false;
        }

        value = parsed;
        return true;
    }
}
