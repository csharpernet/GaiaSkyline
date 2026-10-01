using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using GaiaSkyline.Application.Content;

namespace GaiaSkyline.Web.Seo;

/// <summary>
/// Builds schema.org JSON-LD blocks (serialized to HTML-safe JSON) injected server-side. System.Text.Json
/// escapes &lt;, &gt; and &amp; by default, so the output is safe to embed in a &lt;script&gt; element.
/// </summary>
public static class JsonLd
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string Serialize(object value) => JsonSerializer.Serialize(value, Options);

    public static string LodgingBusiness(
        string pageUrl,
        PropertyDto property,
        string? imageUrl,
        decimal? ratingValue,
        decimal? reviewCount,
        IReadOnlyList<(string Name, bool Available)> amenities,
        IReadOnlyList<ReviewDto> reviews)
    {
        var node = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "LodgingBusiness",
            ["@id"] = pageUrl + "#lodging",
            ["name"] = property.Name,
            ["url"] = pageUrl,
            ["image"] = imageUrl,
            ["priceRange"] = "€€€",
            ["petsAllowed"] = false,
            ["smokingAllowed"] = false,
            ["numberOfRooms"] = property.Bedrooms,
            ["checkinTime"] = property.CheckInFromLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["checkoutTime"] = property.CheckOutByLocal.ToString("HH:mm", CultureInfo.InvariantCulture),
            ["address"] = new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["addressLocality"] = "Vila Nova de Gaia",
                ["addressRegion"] = "Porto",
                ["addressCountry"] = "PT",
            },
            ["geo"] = new Dictionary<string, object?>
            {
                ["@type"] = "GeoCoordinates",
                ["latitude"] = property.Lat,
                ["longitude"] = property.Lng,
            },
        };

        if (amenities.Count > 0)
        {
            node["amenityFeature"] = amenities.Select(a => new Dictionary<string, object?>
            {
                ["@type"] = "LocationFeatureSpecification",
                ["name"] = a.Name,
                ["value"] = a.Available,
            }).ToList();
        }

        if (ratingValue is not null && reviewCount is not null)
        {
            node["aggregateRating"] = new Dictionary<string, object?>
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = ratingValue,
                ["reviewCount"] = reviewCount,
                ["bestRating"] = 5,
                ["worstRating"] = 1,
            };
        }

        if (reviews.Count > 0)
        {
            node["review"] = reviews.Take(6).Select(r => new Dictionary<string, object?>
            {
                ["@type"] = "Review",
                ["author"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = r.GuestFirstName },
                ["reviewBody"] = r.Body,
                ["reviewRating"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Rating",
                    ["ratingValue"] = r.Rating,
                    ["bestRating"] = 5,
                    ["worstRating"] = 1,
                },
            }).ToList();
        }

        return Serialize(node);
    }

    public static string FaqPage(IReadOnlyList<(string Question, string Answer)> faqs)
    {
        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = faqs.Select(f => new Dictionary<string, object?>
            {
                ["@type"] = "Question",
                ["name"] = f.Question,
                ["acceptedAnswer"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Answer",
                    ["text"] = f.Answer,
                },
            }).ToList(),
        });
    }

    public static string Article(string pageUrl, StoryDto story, string? imageUrl, string inLanguage)
    {
        var published = story.PublishedAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Article",
            ["headline"] = story.Title,
            ["description"] = string.IsNullOrWhiteSpace(story.MetaDescription) ? story.Excerpt : story.MetaDescription,
            ["image"] = imageUrl,
            ["datePublished"] = published,
            ["dateModified"] = published,
            ["inLanguage"] = inLanguage,
            ["url"] = pageUrl,
            ["author"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = story.AuthorName },
            ["mainEntityOfPage"] = new Dictionary<string, object?> { ["@type"] = "WebPage", ["@id"] = pageUrl },
        });
    }

    public static string CollectionPage(string pageUrl, string name, IReadOnlyList<(string Url, string Name)> items)
    {
        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "CollectionPage",
            ["name"] = name,
            ["url"] = pageUrl,
            ["mainEntity"] = new Dictionary<string, object?>
            {
                ["@type"] = "ItemList",
                ["itemListElement"] = items.Select((item, index) => new Dictionary<string, object?>
                {
                    ["@type"] = "ListItem",
                    ["position"] = index + 1,
                    ["url"] = item.Url,
                    ["name"] = item.Name,
                }).ToList(),
            },
        });
    }

    public static string BreadcrumbList(IReadOnlyList<(string Name, string? Url)> crumbs)
    {
        return Serialize(new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = crumbs.Select((crumb, index) =>
            {
                var item = new Dictionary<string, object?>
                {
                    ["@type"] = "ListItem",
                    ["position"] = index + 1,
                    ["name"] = crumb.Name,
                };
                if (crumb.Url is not null)
                {
                    item["item"] = crumb.Url;
                }

                return item;
            }).ToList(),
        });
    }
}
