using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace GaiaSkyline.Infrastructure.Documents;

/// <summary>
/// The shared brand language for every generated PDF (Stage 8): the site palette, the embedded Fraunces +
/// Inter fonts (OFL, registered once from assembly resources so rendering is identical everywhere with no
/// CDN/file-system dependency), and the QuestPDF Community licence. Matches the website's design tokens.
/// </summary>
public static class DocumentTheme
{
    // Brand palette — mirrors wwwroot/css/tokens.css.
    public const string Ink = "#0F1417";
    public const string Stone = "#F5F1EA";
    public const string Clay = "#A04A28";
    public const string River = "#2E4F60";
    public const string Fog = "#D9D2C5";
    public const string White = "#FFFFFF";

    public const string Heading = "Fraunces";
    public const string Body = "Inter";

    private static readonly object Gate = new();
    private static bool _initialised;

    /// <summary>Registers the licence + fonts exactly once; safe to call before each document build.</summary>
    public static void EnsureInitialised()
    {
        if (_initialised)
        {
            return;
        }

        lock (Gate)
        {
            if (_initialised)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            var assembly = typeof(DocumentTheme).Assembly;
            foreach (var resource in new[]
                     {
                         "GaiaSkyline.Infrastructure.Documents.Fonts.Inter-Regular.ttf",
                         "GaiaSkyline.Infrastructure.Documents.Fonts.Inter-SemiBold.ttf",
                         "GaiaSkyline.Infrastructure.Documents.Fonts.Fraunces-Regular.ttf",
                         "GaiaSkyline.Infrastructure.Documents.Fonts.Fraunces-SemiBold.ttf",
                     })
            {
                RegisterFont(assembly, resource);
            }

            _initialised = true;
        }
    }

    private static void RegisterFont(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font '{resourceName}' was not found.");
        FontManager.RegisterFontFromStream(stream);
    }
}
