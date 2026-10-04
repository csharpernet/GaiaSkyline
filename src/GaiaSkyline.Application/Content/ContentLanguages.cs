namespace GaiaSkyline.Application.Content;

/// <summary>
/// The five content languages, as BCP-47 tags, in admin display order (English first). Mirrors the web's
/// supported cultures — a test asserts the two stay in sync — but lives here so the content layer can
/// enumerate languages (editor tabs, translation grid) without depending on the web layer.
/// </summary>
public static class ContentLanguages
{
    public const string Default = "en";

    public static readonly IReadOnlyList<string> All = ["en", "pt-PT", "es", "fr", "de"];

    /// <summary>Short uppercase label for the editor tabs / grid columns (e.g. "pt-PT" → "PT").</summary>
    public static string ShortLabel(string languageCode) => languageCode switch
    {
        "en" => "EN",
        "pt-PT" => "PT",
        "es" => "ES",
        "fr" => "FR",
        "de" => "DE",
        _ => languageCode.ToUpperInvariant(),
    };
}
