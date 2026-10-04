namespace GaiaSkyline.Web.Models;

/// <summary>Posted by the content block editor: one entry per language. Which field is meaningful depends
/// on the block's kind, which the server re-reads (never trusted from the client).</summary>
public sealed class ContentEditForm
{
    public List<ContentLanguageInput> Languages { get; set; } = [];

    /// <summary>"save" stages drafts; "publish" stages then promotes them and makes the block live.</summary>
    public string Action { get; set; } = "save";
}

public sealed class ContentLanguageInput
{
    public string LanguageCode { get; set; } = "en";

    /// <summary>Free text / rich-text HTML / URL, or the amenity label for Boolean blocks.</summary>
    public string? Text { get; set; }

    public decimal? Number { get; set; }

    /// <summary>Checkbox: absent when unchecked (false), for Boolean blocks.</summary>
    public bool Boolean { get; set; }

    public Guid? MediaAssetId { get; set; }
}
