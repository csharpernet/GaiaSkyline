using System.Text;
using GaiaSkyline.Application.Content;
using GaiaSkyline.Domain.Content;
using GaiaSkyline.Domain.Entities;
using GaiaSkyline.Domain.Identifiers;
using GaiaSkyline.Domain.Media;
using GaiaSkyline.Domain.Reviews;
using GaiaSkyline.Domain.Stories;
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

    private static readonly ReviewSpec[] Reviews =
    [
        new("Aicha", 5, "Charlotte, North Carolina",
            "Good location, very easy to get into the center of Porto by using bus 901 or 906. Very convenient! The apartment was very modern with a beautiful view. The staff was very nice and even checked on my son when I mentioned he was sick and recommended a pharmacy near by that helped us out.",
            new DateOnly(2026, 8, 1)),
        new("Mary", 5, "Bath, New York",
            "Beatrix was a very good communicator. She gave us good ideas and even walked us around the neighborhood when we were hungry after unpacking. She answered questions promptly. Thank you Beatrix! The hot tub and the views of Porto were relaxing. Washer/dryer and dishwasher made things easier. Local grocery store was a plus. Many choices of fine restaurants within easy Uber rides. Local seafood was fantastic.",
            new DateOnly(2026, 9, 10)),
        new("Pascale", 5, null,
            "There was a little mix-up with the address at the beginning. Ultra-secure building with 1 security guard 24/7. Amazing view. The host offered to book a taxi for us, but ultimately without success.",
            new DateOnly(2026, 9, 17)),
        new("Raquel", 5, null,
            "We had a very nice and quiet stay. The host was extremely attentive throughout the entire stay. When we return to Porto, we will certainly stay here again.",
            new DateOnly(2026, 8, 1)),
        new("Emine", 5, null,
            "Really lovely responsive host, the property was just like the photos and a lovely stay!",
            new DateOnly(2026, 8, 1)),
        new("Patrice", 5, null,
            "Pleasant stay, the view of Porto is beautiful. We had a great stay and were able to enjoy the private pool. The apartment is well-equipped.",
            new DateOnly(2026, 8, 1)),
    ];

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
        await EnsureStoriesAsync(cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _revision.Bump();
    }

    private async Task EnsurePropertyAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Properties.AnyAsync(cancellationToken))
        {
            return;
        }

        // Capacity lives on the Property entity (migration 0003); home.snapshot.line derives its
        // English value from these fields so the numbers have a single source of truth.
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
            checkOutByLocal: new TimeOnly(10, 0),
            sleeps: 6,
            bedrooms: 2,
            beds: 4,
            bathrooms: 2,
            bedsBreakdown: "Bedroom 1 — 2 single beds; Bedroom 2 — 1 queen bed; Living room — 1 sofa bed"));
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
            uploadedBy: Actor,
            altText: $"Gaia Skyline apartment, Vila Nova de Gaia — {label.ToLowerInvariant()}"));
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
        var property = await _dbContext.Properties.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        for (var i = 0; i < Blocks.Length; i++)
        {
            var spec = Blocks[i];
            if (existingKeys.Contains(spec.Key))
            {
                continue;
            }

            // The snapshot line's numbers derive from Property so there is a single source of truth.
            var englishText = spec.Key == "home.snapshot.line" && property is not null
                ? $"{property.Bedrooms} bedrooms · {property.Beds} beds · {property.Bathrooms} baths · " +
                  $"Sleeps {property.Sleeps} · Vila Nova de Gaia"
                : spec.Text;

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

            block.SetTranslation("en", englishText, mediaId, spec.Number, spec.Boolean, SeedTimestampUtc, Actor);

            if (IsTextTranslatable(spec.Kind, englishText))
            {
                foreach (var (language, prefix) in PlaceholderLanguages)
                {
                    block.SetTranslation(
                        language,
                        englishText is null ? null : prefix + englishText,
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

        foreach (var review in Reviews)
        {
            var id = ReviewId.From(DeterministicGuid.From($"review:{review.Guest}"));
            if (existing.Contains(id))
            {
                continue;
            }

            _dbContext.Reviews.Add(new Review(
                id,
                review.Rating,
                review.Guest,
                review.Location,
                review.Body,
                source: "Airbnb",
                review.StayedOn,
                isPublished: true));
        }
    }

    private async Task EnsureStoriesAsync(CancellationToken cancellationToken)
    {
        var property = await _dbContext.Properties.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var authorName = property?.Name ?? "Gaia Skyline";
        var existing = (await _dbContext.Stories.Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();

        for (var i = 0; i < StorySpecs.Length; i++)
        {
            var spec = StorySpecs[i];
            var id = StoryId.From(DeterministicGuid.From($"story:{spec.Slug}"));
            if (existing.Contains(id))
            {
                continue;
            }

            var cover = MediaAssetId.From(DeterministicGuid.From($"media:{spec.CoverMediaName}"));
            var story = new Story(id, spec.Slug, cover, spec.PublishedAtUtc, isPublished: true, displayOrder: i + 1, authorName);

            // English only; other languages fall back to English (owner translates via admin in Stage 7).
            story.SetTranslation(
                "en",
                spec.Title,
                spec.Excerpt,
                spec.Body,
                metaTitle: spec.Title,
                metaDescription: spec.Excerpt,
                readingTimeMinutes: EstimateReadingMinutes(spec.Body));

            _dbContext.Stories.Add(story);
        }
    }

    private static int EstimateReadingMinutes(string html)
    {
        var words = html.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(words / 200.0));
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

    private sealed record ReviewSpec(string Guest, int Rating, string? Location, string Body, DateOnly StayedOn);

    private sealed record StorySpec(
        string Slug,
        string CoverMediaName,
        DateTime PublishedAtUtc,
        string Title,
        string Excerpt,
        string Body);

    // Three keyword-aware example stories. English only for now; the owner expands and translates
    // them via the admin in Stage 7. Bodies are placeholders.
    private static readonly StorySpec[] StorySpecs =
    [
        new("view-from-the-balcony-a-first-timers-guide-to-the-douro",
            "home.gallery.1",
            new DateTime(2026, 9, 26, 9, 0, 0, DateTimeKind.Utc),
            "The View from the Balcony: A First-Timer's Guide to the Douro",
            "What you're actually looking at from the balcony — the Dom Luís I Bridge, both riverbanks, and the best times of day to take it all in.",
            "<p>The first thing most guests do is walk straight to the balcony. It's the right instinct. From here the Douro opens up below you, with the Dom Luís I Bridge to one side and the two riverbanks — Gaia and Porto — laid out in a single view.</p>" +
            "<p>Mornings are the quietest. The light comes up behind the old town and the river turns from slate to silver. By late afternoon the terraces across the water fill up and the whole scene warms to gold.</p>" +
            "<p>This is a placeholder article. The owner will expand it with a proper first-timer's orientation to the river, the bridge decks, and the walks that start right outside the door.</p>"),
        new("why-vila-nova-de-gaia-not-porto-is-the-better-base",
            "home.gallery.2",
            new DateTime(2026, 9, 5, 9, 0, 0, DateTimeKind.Utc),
            "Why Vila Nova de Gaia (Not Porto) Is the Better Base",
            "Staying on the Gaia side of the Douro puts the view in front of you and Porto a short walk across the bridge.",
            "<p>Most visitors assume they should stay in Porto. Stay in Vila Nova de Gaia instead, and the view everyone photographs is the one from your own windows.</p>" +
            "<p>Gaia sits on the south bank of the Douro, directly across from Porto's historic centre. You get the river, the cellars and the calmer streets — and Porto is a short walk across either deck of the bridge whenever you want it.</p>" +
            "<p>This is a placeholder article. The owner will expand it into a practical case for basing a Douro trip on the Gaia side.</p>"),
        new("a-slow-morning-the-hot-tub-sunrise-and-port-wine-at-home",
            "home.gallery.3",
            new DateTime(2026, 8, 12, 9, 0, 0, DateTimeKind.Utc),
            "A Slow Morning: The Hot Tub, Sunrise, and Port Wine at Home",
            "How to spend a morning without leaving the apartment — and why the hot tub at dawn is the best seat in the building.",
            "<p>Some mornings the best plan is no plan. Coffee, the balcony, and the hot tub warm and waiting — the only one in the building — while the city wakes up across the water.</p>" +
            "<p>Set the water somewhere between a refreshing 27°C and a spa-warm 33°C, watch the sun come up over the old town, and save a glass of Port for when the lights come on again that evening.</p>" +
            "<p>This is a placeholder article. The owner will expand it into a slow-morning ritual guide for the apartment.</p>"),
    ];

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

        // ----- amenities (Boolean = availability; Text = label). Full source list; the
        //       not_available group renders greyed out (ValueBoolean = false). -----
        new("amenities", "amenities.views.city_skyline", ContentKind.Boolean, "Amenity: City skyline view", Text: "City skyline view", Boolean: true),
        new("amenities", "amenities.views.river_view", ContentKind.Boolean, "Amenity: River view", Text: "River view", Boolean: true),

        new("amenities", "amenities.bathroom.bathtub", ContentKind.Boolean, "Amenity: Bathtub", Text: "Bathtub", Boolean: true),
        new("amenities", "amenities.bathroom.hair_dryer", ContentKind.Boolean, "Amenity: Hair dryer", Text: "Hair dryer", Boolean: true),
        new("amenities", "amenities.bathroom.shampoo", ContentKind.Boolean, "Amenity: Shampoo", Text: "Shampoo", Boolean: true),
        new("amenities", "amenities.bathroom.conditioner", ContentKind.Boolean, "Amenity: Conditioner", Text: "Conditioner", Boolean: true),
        new("amenities", "amenities.bathroom.body_soap", ContentKind.Boolean, "Amenity: Body soap", Text: "Body soap", Boolean: true),
        new("amenities", "amenities.bathroom.bidet", ContentKind.Boolean, "Amenity: Bidet", Text: "Bidet", Boolean: true),
        new("amenities", "amenities.bathroom.hot_water", ContentKind.Boolean, "Amenity: Hot water", Text: "Hot water", Boolean: true),
        new("amenities", "amenities.bathroom.shower_gel", ContentKind.Boolean, "Amenity: Shower gel", Text: "Shower gel", Boolean: true),

        new("amenities", "amenities.bedroom_laundry.washer", ContentKind.Boolean, "Amenity: Washer", Text: "Washer", Boolean: true),
        new("amenities", "amenities.bedroom_laundry.hangers", ContentKind.Boolean, "Amenity: Hangers", Text: "Hangers", Boolean: true),
        new("amenities", "amenities.bedroom_laundry.bed_linens", ContentKind.Boolean, "Amenity: Bed linens", Text: "Bed linens", Boolean: true),
        new("amenities", "amenities.bedroom_laundry.extra_pillows_blankets", ContentKind.Boolean, "Amenity: Extra pillows and blankets", Text: "Extra pillows and blankets", Boolean: true),
        new("amenities", "amenities.bedroom_laundry.room_darkening_shades", ContentKind.Boolean, "Amenity: Room-darkening shades", Text: "Room-darkening shades", Boolean: true),
        new("amenities", "amenities.bedroom_laundry.iron", ContentKind.Boolean, "Amenity: Iron", Text: "Iron", Boolean: true),
        new("amenities", "amenities.bedroom_laundry.clothing_storage", ContentKind.Boolean, "Amenity: Clothing storage", Text: "Clothing storage", Boolean: true),

        new("amenities", "amenities.entertainment.tv", ContentKind.Boolean, "Amenity: TV", Text: "TV", Boolean: true),

        new("amenities", "amenities.climate.air_conditioning", ContentKind.Boolean, "Amenity: Air conditioning", Text: "Air conditioning", Boolean: true),
        new("amenities", "amenities.climate.heating", ContentKind.Boolean, "Amenity: Heating", Text: "Heating", Boolean: true),

        new("amenities", "amenities.safety.fire_extinguisher", ContentKind.Boolean, "Amenity: Fire extinguisher", Text: "Fire extinguisher", Boolean: true),
        new("amenities", "amenities.safety.first_aid_kit", ContentKind.Boolean, "Amenity: First aid kit", Text: "First aid kit", Boolean: true),

        new("amenities", "amenities.internet_office.wifi", ContentKind.Boolean, "Amenity: Wifi", Text: "Wifi", Boolean: true),
        new("amenities", "amenities.internet_office.workspace", ContentKind.Boolean, "Amenity: Dedicated workspace", Text: "Dedicated workspace", Boolean: true),

        new("amenities", "amenities.kitchen.kitchen", ContentKind.Boolean, "Amenity: Kitchen", Text: "Kitchen", Boolean: true),
        new("amenities", "amenities.kitchen.refrigerator", ContentKind.Boolean, "Amenity: Refrigerator", Text: "Refrigerator", Boolean: true),
        new("amenities", "amenities.kitchen.microwave", ContentKind.Boolean, "Amenity: Microwave", Text: "Microwave", Boolean: true),
        new("amenities", "amenities.kitchen.cooking_basics", ContentKind.Boolean, "Amenity: Cooking basics", Text: "Cooking basics", Boolean: true),
        new("amenities", "amenities.kitchen.dishes_silverware", ContentKind.Boolean, "Amenity: Dishes and silverware", Text: "Dishes and silverware", Boolean: true),
        new("amenities", "amenities.kitchen.freezer", ContentKind.Boolean, "Amenity: Freezer", Text: "Freezer", Boolean: true),
        new("amenities", "amenities.kitchen.dishwasher", ContentKind.Boolean, "Amenity: Dishwasher", Text: "Dishwasher", Boolean: true),
        new("amenities", "amenities.kitchen.stove", ContentKind.Boolean, "Amenity: Stove", Text: "Stove", Boolean: true),
        new("amenities", "amenities.kitchen.oven", ContentKind.Boolean, "Amenity: Oven", Text: "Oven", Boolean: true),
        new("amenities", "amenities.kitchen.hot_water_kettle", ContentKind.Boolean, "Amenity: Hot water kettle", Text: "Hot water kettle", Boolean: true),
        new("amenities", "amenities.kitchen.coffee_maker", ContentKind.Boolean, "Amenity: Coffee maker", Text: "Coffee maker", Boolean: true),
        new("amenities", "amenities.kitchen.wine_glasses", ContentKind.Boolean, "Amenity: Wine glasses", Text: "Wine glasses", Boolean: true),
        new("amenities", "amenities.kitchen.toaster", ContentKind.Boolean, "Amenity: Toaster", Text: "Toaster", Boolean: true),
        new("amenities", "amenities.kitchen.baking_sheet", ContentKind.Boolean, "Amenity: Baking sheet", Text: "Baking sheet", Boolean: true),
        new("amenities", "amenities.kitchen.dining_table", ContentKind.Boolean, "Amenity: Dining table", Text: "Dining table", Boolean: true),
        new("amenities", "amenities.kitchen.coffee", ContentKind.Boolean, "Amenity: Coffee", Text: "Coffee", Boolean: true),

        new("amenities", "amenities.outdoor.patio_balcony", ContentKind.Boolean, "Amenity: Patio or balcony", Text: "Patio or balcony", Boolean: true),
        new("amenities", "amenities.outdoor.outdoor_furniture", ContentKind.Boolean, "Amenity: Outdoor furniture", Text: "Outdoor furniture", Boolean: true),
        new("amenities", "amenities.outdoor.outdoor_dining", ContentKind.Boolean, "Amenity: Outdoor dining area", Text: "Outdoor dining area", Boolean: true),

        new("amenities", "amenities.parking.free_parking", ContentKind.Boolean, "Amenity: Free parking on premises", Text: "Free parking on premises", Boolean: true),
        new("amenities", "amenities.parking.pool", ContentKind.Boolean, "Amenity: Pool", Text: "Pool", Boolean: true),
        new("amenities", "amenities.parking.hot_tub", ContentKind.Boolean, "Amenity: Private hot tub", Text: "Private hot tub (available all year, 24h)", Boolean: true),

        new("amenities", "amenities.services.host_greeting", ContentKind.Boolean, "Amenity: Host greets you", Text: "Host greets you", Boolean: true),

        new("amenities", "amenities.not_available.essentials", ContentKind.Boolean, "Not available: Essentials", Text: "Essentials (disposable plastic toiletries eliminated)", Boolean: false),
        new("amenities", "amenities.not_available.smoke_alarm", ContentKind.Boolean, "Not available: Smoke alarm", Text: "Smoke alarm", Boolean: false),
        new("amenities", "amenities.not_available.carbon_monoxide_alarm", ContentKind.Boolean, "Not available: Carbon monoxide alarm", Text: "Carbon monoxide alarm", Boolean: false),
        new("amenities", "amenities.not_available.private_entrance", ContentKind.Boolean, "Not available: Private entrance", Text: "Private entrance", Boolean: false),
    ];
}
