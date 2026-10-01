using System.Security;
using System.Text;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Web.Localization;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>Multi-language sitemap and robots.txt.</summary>
public sealed class SeoController(IContentService content) : Controller
{
    private static readonly string[] StaticPaths =
    [
        "",
        "gallery",
        "book",
        "stories",
        "legal/terms",
        "legal/privacy",
        "legal/cancellation-policy",
        "legal/al-registration",
    ];

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
    {
        var stories = await content.GetPublishedStoriesAsync(SupportedCultures.DefaultCulture, take: null, cancellationToken);
        var paths = StaticPaths.Concat(stories.Select(s => $"stories/{s.Slug}")).ToList();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        builder.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">\n");

        foreach (var path in paths)
        {
            foreach (var culture in SupportedCultures.All)
            {
                builder.Append("  <url>\n");
                builder.Append(System.Globalization.CultureInfo.InvariantCulture, $"    <loc>{Escape(AbsoluteUrl(baseUrl, culture.Slug, path))}</loc>\n");
                foreach (var alternate in SupportedCultures.All)
                {
                    builder.Append(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"    <xhtml:link rel=\"alternate\" hreflang=\"{alternate.Culture}\" href=\"{Escape(AbsoluteUrl(baseUrl, alternate.Slug, path))}\" />\n");
                }

                builder.Append(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"    <xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"{Escape(AbsoluteUrl(baseUrl, SupportedCultures.DefaultSlug, path))}\" />\n");
                builder.Append("  </url>\n");
            }
        }

        builder.Append("</urlset>\n");
        return Content(builder.ToString(), "application/xml", Encoding.UTF8);
    }

    [HttpGet("/robots.txt")]
    public IActionResult Robots()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var body =
            "User-agent: *\n" +
            "Allow: /\n" +
            "Disallow: /admin/\n" +
            "Disallow: /api/\n" +
            "Disallow: /partners/dashboard/\n" +
            "Disallow: /my/\n" +
            "Disallow: /webhooks/\n" +
            "\n" +
            $"Sitemap: {baseUrl}/sitemap.xml\n";
        return Content(body, "text/plain", Encoding.UTF8);
    }

    private static string AbsoluteUrl(string baseUrl, string slug, string path) =>
        string.IsNullOrEmpty(path) ? $"{baseUrl}/{slug}" : $"{baseUrl}/{slug}/{path}";

    private static string Escape(string url) => SecurityElement.Escape(url) ?? url;
}
