namespace GaiaSkyline.Domain.Content;

/// <summary>
/// The shape of a content block's value. Determines which column of a
/// <see cref="ContentTranslation"/> carries the value and how the public site renders it.
/// </summary>
public enum ContentKind
{
    PlainText,
    RichText,
    ShortText,
    Url,
    ImageRef,
    VideoRef,
    Number,
    Boolean,
}
