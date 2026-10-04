namespace GaiaSkyline.Application.Content;

/// <summary>
/// Sanitizes owner-authored rich-text HTML against a strict allowlist before it is stored, so a RichText
/// block can never carry script/style/event handlers onto the public site. Implemented in Infrastructure.
/// </summary>
public interface IHtmlContentSanitizer
{
    string Sanitize(string? html);
}
