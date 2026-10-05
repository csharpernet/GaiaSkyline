using System.Security;
using System.Text;
using GaiaSkyline.Web.Localization;
using GaiaSkyline.Web.Seo;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>Multi-language sitemap and robots.txt.</summary>
public sealed class SeoController(SitemapBuilder sitemap) : Controller
{
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var entries = await sitemap.BuildAsync(baseUrl, cancellationToken);

        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        builder.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">\n");

        foreach (var entry in entries)
        {
            // A page the owner marked noindex (per language) is dropped from the sitemap; a page that is
            // noindex in every language is omitted entirely, and hreflang alternates list only the indexable ones.
            var included = entry.Languages.Where(l => l.Included).ToList();
            if (included.Count == 0)
            {
                continue;
            }

            var xDefault = included.FirstOrDefault(l => l.Culture == SupportedCultures.DefaultCulture) ?? included[0];
            foreach (var language in included)
            {
                builder.Append("  <url>\n");
                builder.Append(System.Globalization.CultureInfo.InvariantCulture, $"    <loc>{Escape(language.Url)}</loc>\n");
                foreach (var alternate in included)
                {
                    builder.Append(
                        System.Globalization.CultureInfo.InvariantCulture,
                        $"    <xhtml:link rel=\"alternate\" hreflang=\"{alternate.Culture}\" href=\"{Escape(alternate.Url)}\" />\n");
                }

                builder.Append(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"    <xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"{Escape(xDefault.Url)}\" />\n");
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
            "Disallow: /account/\n" +
            "Disallow: /*/account/\n" +
            "Disallow: /partners/dashboard/\n" +
            "Disallow: /my/\n" +
            "Disallow: /*/my/\n" +
            "Disallow: /calendar/\n" +
            "Disallow: /webhooks/\n" +
            "\n" +
            $"Sitemap: {baseUrl}/sitemap.xml\n";
        return Content(body, "text/plain", Encoding.UTF8);
    }

    private static string Escape(string url) => SecurityElement.Escape(url) ?? url;
}
