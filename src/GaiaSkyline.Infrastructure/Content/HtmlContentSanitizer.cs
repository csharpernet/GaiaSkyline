using GaiaSkyline.Application.Content;
using Ganss.Xss;

namespace GaiaSkyline.Infrastructure.Content;

/// <summary>
/// HtmlSanitizer-backed allowlist sanitizer for RichText content: only basic formatting, lists, headings,
/// blockquotes and safe links survive. Links are forced to rel="noopener noreferrer" and http(s)/mailto
/// schemes only; everything else (script, style, event handlers, iframes, inline styles) is stripped.
/// Configured once and reused — HtmlSanitizer.Sanitize is thread-safe.
/// </summary>
internal sealed class HtmlContentSanitizer : IHtmlContentSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    private static readonly string[] Tags =
        ["p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "a", "h2", "h3", "blockquote"];

    public HtmlContentSanitizer()
    {
        // Start from the library defaults (which keep href/src registered as URI attributes so their
        // scheme is checked), then narrow tags/attributes/schemes to our allowlist.
        _sanitizer = new HtmlSanitizer();
        _sanitizer.AllowedTags.Clear();
        foreach (var tag in Tags)
        {
            _sanitizer.AllowedTags.Add(tag);
        }

        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedAttributes.Add("href");
        _sanitizer.AllowedAttributes.Add("title");

        _sanitizer.AllowedSchemes.Clear();
        _sanitizer.AllowedSchemes.Add("http");
        _sanitizer.AllowedSchemes.Add("https");
        _sanitizer.AllowedSchemes.Add("mailto");

        _sanitizer.AllowedCssProperties.Clear();

        // Harden outbound links.
        _sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Html.Dom.IHtmlAnchorElement anchor)
            {
                anchor.SetAttribute("rel", "noopener noreferrer");
            }
        };
    }

    public string Sanitize(string? html) =>
        string.IsNullOrWhiteSpace(html) ? string.Empty : _sanitizer.Sanitize(html);
}
