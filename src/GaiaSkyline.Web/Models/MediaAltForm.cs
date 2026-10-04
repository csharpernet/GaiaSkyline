namespace GaiaSkyline.Web.Models;

/// <summary>Posted by the media editor: one alt-text entry per language. A blank value clears that language.</summary>
public sealed class MediaAltForm
{
    public List<MediaAltInput> Alts { get; set; } = [];
}

public sealed class MediaAltInput
{
    public string LanguageCode { get; set; } = "en";

    public string? Text { get; set; }
}
