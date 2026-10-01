using System.Text;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GaiaSkyline.Infrastructure.Data;

/// <summary>
/// Idempotently seeds the canonical content (see docs/content-seed.md) on Development startup.
/// English is authoritative; the other four languages get visible placeholder translations
/// (English prefixed with [PT]/[ES]/[FR]/[DE]) for text blocks, while non-text blocks are seeded
/// in English only so requests for those languages exercise the English fallback (ADR 0007).
/// </summary>
public sealed class ContentSeeder(AppDbContext dbContext, IContentRevision revision)
{
    private const string Actor = "seed";
    private const int GalleryImageCount = 10;
    private static readonly DateTime SeedTimestampUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly (string Language, string Prefix)[] PlaceholderLanguages =
    [
        ("pt-PT", "[PT] "),
        ("es", "[ES] "),
        ("fr", "[FR] "),
        ("de", "[DE] "),
    ];

    private static readonly string[] ReviewGuests =
        ["Aicha", "Mary", "Pascale", "Raquel", "Emine", "Patrice"];

    private readonly AppDbContext _dbContext = dbContext;
    private readonly IContentRevision _revision = revision;

    public async Task SeedAsync(string? mediaPhysicalRoot, CancellationToken cancellationToken)
    {
        await EnsurePropertyAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureMediaAssetsAsync(mediaPhysicalRoot, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await EnsureGalleryAsync(cancellationToken);
        await EnsureContentBlocksAsync(cancellationToken);
        await EnsureReviewsAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _revision.Bump();
    }

    private async Task EnsurePropertyAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Properties.AnyAsync(cancellationToken))
        {
            return;
        }

        // The Stage 1 Property entity carries no capacity fields; the sleeps/beds/baths figures from
        // the brief live in content blocks (home.snapshot.line, amenities). See docs/content-seed.md.
        _dbContext.Properties.Add(new Property(
            PropertyId.From(DeterministicGuid.From("property:gaia-skyline")),
            name: "Gaia Skyline Apartment",
            registrationCode: "175890/AL",
            address: "Vila Nova de Gaia, Portugal",
            lat: 41.1370,
            lng: -8.6076,
            defaultCurrency: "EUR",
            timezone: "Europe/Lisbon",
            checkInFromLocal: new TimeOnly(16, 0),
            checkOutByLocal: new TimeOnly(10, 0)));
    }

    private async Task EnsureMediaAssetsAsync(string? mediaPhysicalRoot, CancellationToken cancellationToken)
    {
        if (mediaPhysicalRoot is not null)
        {
            Directory.CreateDirectory(mediaPhysicalRoot);
        }

        var existing = (await _dbContext.MediaAssets.Select(a => a.Id).ToListAsync(cancellationToken)).ToHashSet();

        var posterId = MediaAssetId.From(DeterministicGuid.From("media:home.hero.poster"));
        AddImageAsset(existing, posterId, "Hero poster", mediaPhysicalRoot);

        for (var i = 1; i <= GalleryImageCount; i++)
        {
            var id = MediaAssetId.From(DeterministicGuid.From($"media:home.gallery.{i}"));
            AddImageAsset(existing, id, $"Gallery image {i}", mediaPhysicalRoot);
        }

        // Hero video placeholder: no binary is generated in this stage; the owner uploads the real
        // file via the admin in Stage 7. The poster points at the seeded poster image.
        var videoId = MediaAssetId.From(DeterministicGuid.From("media:home.hero.video"));
        if (!existing.Contains(videoId))
        {
            _dbContext.MediaAssets.Add(new MediaAsset(
                videoId,
                MediaKind.Video,
                blobUri: $"/media/{videoId.Value:N}.mp4",
                posterBlobUri: $"/media/{posterId.Value:N}.svg",
                width: 1920,
                height: 1080,
                durationSec: 30,
                byteSize: 0,
                contentType: "video/mp4",
                uploadedAtUtc: SeedTimestampUtc,
                uploadedBy: Actor));
        }
    }

    private void AddImageAsset(
        HashSet<MediaAssetId> existing,
        MediaAssetId id,
        string label,
        string? mediaPhysicalRoot)
    {
        var fileName = $"{id.Value:N}.svg";
        var svg = PlaceholderSvg(label);

        if (mediaPhysicalRoot is not null)
        {
            var path = Path.Combine(mediaPhysicalRoot, fileName);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, svg, Encoding.UTF8);
            }
        }

        if (existing.Contains(id))
        {
            return;
        }

        _dbContext.MediaAssets.Add(new MediaAsset(
            id,
            MediaKind.Image,
            blobUri: $"/media/{fileName}",
            posterBlobUri: null,
            width: 1600,
            height: 1066,
            durationSec: null,
            byteSize: Encoding.UTF8.GetByteCount(svg),
            contentType: "image/svg+xml",
            uploadedAtUtc: SeedTimestampUtc,
            uploadedBy: Actor));
    }

    private async Task EnsureGalleryAsync(CancellationToken cancellationToken)
    {
        var collectionId = MediaCollectionId.From(DeterministicGuid.From("collection:home.gallery"));
        if (await _dbContext.MediaCollections.AnyAsync(c => c.Id == collectionId, cancellationToken))
        {
            return;
        }

        var collection = new MediaCollection(collectionId, "home.gallery", "Home gallery");
        for (var i = 1; i <= GalleryImageCount; i++)
        {
            collection.AddItem(
                MediaCollectionItemId.From(DeterministicGuid.From($"collectionitem:home.gallery:{i}")),
                MediaAssetId.From(DeterministicGuid.From($"media:home.gallery.{i}")),
                displayOrder: i,
                isHero: i == 1);
        }

        _dbContext.MediaCollections.Add(collection);
    }

    private async Task EnsureContentBlocksAsync(CancellationToken cancellationToken)
    {
        var existingKeys = (await _dbContext.ContentBlocks.Select(b => b.Key).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        for (var i = 0; i < Blocks.Length; i++)
        {
            var spec = Blocks[i];
            if (existingKeys.Contains(spec.Key))
            {
                continue;
            }

            var block = new ContentBlock(
                ContentBlockId.From(DeterministicGuid.From($"block:{spec.Key}")),
                spec.Key,
                spec.Kind,
                spec.Section,
                spec.DisplayName,
                displayOrder: i + 1,
                isPublished: true,
                SeedTimestampUtc,
                Actor);

            var mediaId = spec.MediaName is null
                ? (MediaAssetId?)null
                : MediaAssetId.From(DeterministicGuid.From($"media:{spec.MediaName}"));

            block.SetTranslation("en", spec.Text, mediaId, spec.Number, spec.Boolean, SeedTimestampUtc, Actor);

            if (IsTextTranslatable(spec.Kind, spec.Text))
            {
                foreach (var (language, prefix) in PlaceholderLanguages)
                {
                    block.SetTranslation(
                        language,
                        spec.Text is null ? null : prefix + spec.Text,
                        mediaId,
                        spec.Number,
                        spec.Boolean,
                        SeedTimestampUtc,
                        Actor);
                }
            }

            _dbContext.ContentBlocks.Add(block);
        }
    }

    private async Task EnsureReviewsAsync(CancellationToken cancellationToken)
    {
        var existing = (await _dbContext.Reviews.Select(r => r.Id).ToListAsync(cancellationToken)).ToHashSet();

        for (var i = 0; i < ReviewGuests.Length; i++)
        {
            var name = ReviewGuests[i];
            var id = ReviewId.From(DeterministicGuid.From($"review:{name}"));
            if (existing.Contains(id))
            {
                continue;
            }

            // Review bodies/ratings/locations were not supplied in the Stage 2 brief; they are
            // placeholders to be replaced from the real listing via the admin (ADR 0007).
            _dbContext.Reviews.Add(new Review(
                id,
                rating: 5,
                guestFirstName: name,
                guestLocation: null,
                body: "TBD",
                source: "Airbnb",
                stayedOn: new DateOnly(2025, i + 1, 15),
                isPublished: true));
        }
    }

    private static bool IsTextTranslatable(ContentKind kind, string? text) => kind switch
    {
        ContentKind.PlainText or ContentKind.RichText or ContentKind.ShortText => true,
        ContentKind.Boolean => !string.IsNullOrWhiteSpace(text), // amenity labels
        _ => false,
    };

    private static string PlaceholderSvg(string label) =>
        "<svg xmlns='http://www.w3.org/2000/svg' width='1600' height='1066' viewBox='0 0 1600 1066'>" +
        "<rect width='100%' height='100%' fill='#D9D2C5'/>" +
        "<text x='50%' y='50%' font-family='sans-serif' font-size='56' fill='#2E4F60' " +
        $"text-anchor='middle' dominant-baseline='middle'>{label}</text></svg>";

    private sealed record BlockSpec(
        string Section,
        string Key,
        ContentKind Kind,
        string DisplayName,
        string? Text = null,
        decimal? Number = null,
        bool? Boolean = null,
        string? MediaName = null);

    // The canonical content. Keep in sync with docs/content-seed.md.
    private static readonly BlockSpec[] Blocks =
    [
        // ----- home: hero -----
        new("home", "home.hero.headline", ContentKind.PlainText, "Hero headline",
            Text: "Wake up to the Dom Luís I Bridge"),
        new("home", "home.hero.subheadline", ContentKind.PlainText, "Hero subheadline",
            Text: "A two-bedroom apartment above the Douro — with the only true hot tub in the building."),
        new("home", "home.hero.cta_text", ContentKind.ShortText, "Hero CTA text",
            Text: "Check availability"),
        new("home", "home.hero.cta_href", ContentKind.Url, "Hero CTA link",
            Text: "/book"),
        new("home", "home.hero.overlay_opacity", ContentKind.Number, "Hero overlay opacity",
            Number: 0.35m),
        new("home", "home.hero.video", ContentKind.VideoRef, "Hero video",
            MediaName: "home.hero.video"),
        new("home", "home.hero.poster", ContentKind.ImageRef, "Hero poster image",
            MediaName: "home.hero.poster"),

        // ----- home: snapshot -----
        new("home", "home.snapshot.line", ContentKind.PlainText, "Snapshot line",
            Text: "2 bedrooms · 4 beds · 2 baths · Sleeps 6 · Vila Nova de Gaia"),

        // ----- home: the view -----
        new("home", "home.view.title", ContentKind.PlainText, "View title",
            Text: "The view"),
        new("home", "home.view.body", ContentKind.RichText, "View body",
            Text: "The living room and both bedrooms open directly onto a generous balcony facing the Dom Luís I Bridge — not every apartment here does. The Douro unfolds below, with both the Vila Nova de Gaia and Porto riverbanks in one sweeping panorama. The apartment also sits at the building's sweet spot: high enough for open, unobstructed views, low enough that the bridge and the river still feel close rather than distant. Sunrises from this balcony are unforgettable."),

        // ----- home: the hot tub -----
        new("home", "home.hottub.title", ContentKind.PlainText, "Hot tub title",
            Text: "The hot tub"),
        new("home", "home.hottub.body", ContentKind.RichText, "Hot tub body",
            Text: "Step outside and sink into a genuine hot tub — currently the only one in the building. Set the water anywhere between a refreshing 27 °C and a spa-warm 33 °C, so it's a proper warm soak in any season: morning coffee at dawn, a glass of Port as the city lights come on. Professionally maintained on a regular schedule, it's always clean, balanced and ready when you are."),

        // ----- home: the layout -----
        new("home", "home.layout.title", ContentKind.PlainText, "Layout title",
            Text: "The layout"),
        new("home", "home.layout.body", ContentKind.RichText, "Layout body",
            Text: "Inside, the kitchen and living area flow together as one continuous, light-filled space — the most open-plan two-bedroom layout in the building, and noticeably airier and brighter for it. Sophisticated, balanced décor lets the view take the lead. The kitchen is fully equipped, and you'll also find a washing machine, iron, and a dedicated workspace with fast Wi-Fi."),

        // ----- home: highlights -----
        new("home", "home.highlights.1.title", ContentKind.ShortText, "Highlight 1 title",
            Text: "Guest Favorite"),
        new("home", "home.highlights.1.body", ContentKind.PlainText, "Highlight 1 body",
            Text: "Rated 4.91 out of 5 across 23 reviews"),
        new("home", "home.highlights.2.title", ContentKind.ShortText, "Highlight 2 title",
            Text: "Perfect ratings from families"),
        new("home", "home.highlights.2.body", ContentKind.PlainText, "Highlight 2 body",
            Text: "100% of families rated it 5 stars in the past year"),
        new("home", "home.highlights.3.title", ContentKind.ShortText, "Highlight 3 title",
            Text: "Only true hot tub in the building"),
        new("home", "home.highlights.3.body", ContentKind.PlainText, "Highlight 3 body",
            Text: "Heated 27–33 °C, all year"),

        // ----- home: reviews aggregate + subscores (numbers: English only -> fall back) -----
        new("home", "home.reviews.aggregate.value", ContentKind.Number, "Reviews average", Number: 4.91m),
        new("home", "home.reviews.aggregate.count", ContentKind.Number, "Reviews count", Number: 23m),
        new("home", "home.reviews.subscores.cleanliness", ContentKind.Number, "Subscore cleanliness", Number: 4.9m),
        new("home", "home.reviews.subscores.accuracy", ContentKind.Number, "Subscore accuracy", Number: 4.9m),
        new("home", "home.reviews.subscores.checkin", ContentKind.Number, "Subscore check-in", Number: 4.9m),
        new("home", "home.reviews.subscores.communication", ContentKind.Number, "Subscore communication", Number: 4.9m),
        new("home", "home.reviews.subscores.location", ContentKind.Number, "Subscore location", Number: 4.6m),
        new("home", "home.reviews.subscores.value", ContentKind.Number, "Subscore value", Number: 4.8m),

        // ----- home: location -----
        new("home", "home.location.blurb", ContentKind.RichText, "Location blurb",
            Text: "Vila Nova de Gaia sits on the south bank of the Douro, directly across the water from Porto's historic centre. The riverside is an easy, walkable stretch, with boats on the river below and the Dom Luís I Bridge linking the two cities on two levels. It makes a relaxed base for exploring both banks of the Douro on foot."),

        // ----- home: host -----
        new("home", "home.host.name", ContentKind.ShortText, "Host name", Text: "Home Me"),
        new("home", "home.host.years", ContentKind.Number, "Host years hosting", Number: 11m),
        new("home", "home.host.languages", ContentKind.ShortText, "Host languages",
            Text: "English, French, Portuguese, Spanish"),

        // ----- rules -----
        new("rules", "rules.checkin_window", ContentKind.ShortText, "Check-in window", Text: "16:00 – 23:00"),
        new("rules", "rules.checkout_by", ContentKind.ShortText, "Check-out by", Text: "10:00"),
        new("rules", "rules.late_checkin_fee_eur", ContentKind.Number, "Late check-in fee (EUR)", Number: 30m),
        new("rules", "rules.max_guests", ContentKind.Number, "Maximum guests", Number: 6m),
        new("rules", "rules.smoking", ContentKind.PlainText, "Smoking policy",
            Text: "No smoking inside — balcony only."),
        new("rules", "rules.quiet_hours", ContentKind.PlainText, "Quiet hours",
            Text: "23:00 – 08:00. No parties."),
        new("rules", "rules.children_note", ContentKind.PlainText, "Children note",
            Text: "Not suitable for children and infants."),

        // ----- footer -----
        new("footer", "footer.registration_label", ContentKind.ShortText, "Registration label", Text: "AL Registration"),
        new("footer", "footer.registration_value", ContentKind.ShortText, "Registration value", Text: "175890/AL"),

        // ----- faq (questions and answers are TBD placeholders; owner fills via admin) -----
        new("faq", "faq.1.q", ContentKind.ShortText, "FAQ 1 question", Text: "TBD"),
        new("faq", "faq.1.a", ContentKind.RichText, "FAQ 1 answer", Text: "TBD"),
        new("faq", "faq.2.q", ContentKind.ShortText, "FAQ 2 question", Text: "TBD"),
        new("faq", "faq.2.a", ContentKind.RichText, "FAQ 2 answer", Text: "TBD"),
        new("faq", "faq.3.q", ContentKind.ShortText, "FAQ 3 question", Text: "TBD"),
        new("faq", "faq.3.a", ContentKind.RichText, "FAQ 3 answer", Text: "TBD"),
        new("faq", "faq.4.q", ContentKind.ShortText, "FAQ 4 question", Text: "TBD"),
        new("faq", "faq.4.a", ContentKind.RichText, "FAQ 4 answer", Text: "TBD"),
        new("faq", "faq.5.q", ContentKind.ShortText, "FAQ 5 question", Text: "TBD"),
        new("faq", "faq.5.a", ContentKind.RichText, "FAQ 5 answer", Text: "TBD"),
        new("faq", "faq.6.q", ContentKind.ShortText, "FAQ 6 question", Text: "TBD"),
        new("faq", "faq.6.a", ContentKind.RichText, "FAQ 6 answer", Text: "TBD"),

        // ----- amenities (Boolean = availability; Text = label). Grounded in the provided
        //       description; groups without source data are intentionally omitted (see seed doc). -----
        new("amenities", "amenities.views.bridge_balcony", ContentKind.Boolean, "Amenity: bridge balcony",
            Text: "Balcony facing the Dom Luís I Bridge", Boolean: true),
        new("amenities", "amenities.views.river_panorama", ContentKind.Boolean, "Amenity: river panorama",
            Text: "Douro river & city panorama", Boolean: true),
        new("amenities", "amenities.outdoor.hot_tub", ContentKind.Boolean, "Amenity: hot tub",
            Text: "Private hot tub (27–33 °C)", Boolean: true),
        new("amenities", "amenities.outdoor.balcony", ContentKind.Boolean, "Amenity: balcony",
            Text: "Private balcony", Boolean: true),
        new("amenities", "amenities.kitchen.fully_equipped", ContentKind.Boolean, "Amenity: kitchen",
            Text: "Fully equipped kitchen", Boolean: true),
        new("amenities", "amenities.bedroom_laundry.washing_machine", ContentKind.Boolean, "Amenity: washing machine",
            Text: "Washing machine", Boolean: true),
        new("amenities", "amenities.bedroom_laundry.iron", ContentKind.Boolean, "Amenity: iron",
            Text: "Iron", Boolean: true),
        new("amenities", "amenities.internet_office.wifi", ContentKind.Boolean, "Amenity: Wi-Fi",
            Text: "Fast Wi-Fi", Boolean: true),
        new("amenities", "amenities.internet_office.workspace", ContentKind.Boolean, "Amenity: workspace",
            Text: "Dedicated workspace", Boolean: true),
        new("amenities", "amenities.bathroom.two_bathrooms", ContentKind.Boolean, "Amenity: bathrooms",
            Text: "2 bathrooms", Boolean: true),
        new("amenities", "amenities.services.hot_tub_maintenance", ContentKind.Boolean, "Amenity: hot tub maintenance",
            Text: "Professionally maintained hot tub", Boolean: true),
        new("amenities", "amenities.not_available.children", ContentKind.Boolean, "Not available: children",
            Text: "Suitable for children and infants", Boolean: false),
        new("amenities", "amenities.not_available.smoking_indoors", ContentKind.Boolean, "Not available: indoor smoking",
            Text: "Smoking indoors", Boolean: false),
    ];
}
