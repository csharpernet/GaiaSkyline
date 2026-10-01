# Content seed — canonical source

This document is the human-readable source of truth for the content seeded by
`src/GaiaSkyline.Infrastructure/Data/ContentSeeder.cs`. Keep the two in sync. The seeder runs on
**Development** startup, is **idempotent** (stable, content-derived GUIDs), and seeds English as
authoritative plus placeholder translations for the other four languages.

## Languages & resolution

Supported: **en** (default/fallback), **pt-PT**, **es**, **fr**, **de**. The BCP-47 tag is stored
as-is. Per request, language resolves in order: `?lang=` → cookie `.AspNetCore.Culture` →
`Accept-Language` → **en**.

**Placeholder translations (ADR 0007):** text blocks are seeded in English and in the four other
languages as the English value prefixed with `[PT] `, `[ES] `, `[FR] `, `[DE] `. **Non-text blocks**
(Url, Number, media refs) are seeded in **English only**, so a request for another language
deliberately falls back to English — this exercises the fallback path. If a value is missing in
both the requested language and English, the API returns the key wrapped in `‹key›`.

## Property (singleton)

Seeded onto the `Property` entity, which now carries capacity columns (migration 0003).

| Field | Value |
| --- | --- |
| Name | Gaia Skyline Apartment |
| Registration | 175890/AL |
| Address | Vila Nova de Gaia, Portugal |
| Lat / Lng | 41.1370 / -8.6076 |
| Default currency | EUR |
| Timezone | Europe/Lisbon |
| Check-in from | 16:00 |
| Check-out by | 10:00 |
| Sleeps | 6 |
| Bedrooms | 2 |
| Beds | 4 |
| Bathrooms | 2 |
| BedsBreakdown | Bedroom 1 — 2 single beds; Bedroom 2 — 1 queen bed; Living room — 1 sofa bed |

`home.snapshot.line`'s English value is **derived from these Property fields on first seed**
(`{Bedrooms} bedrooms · {Beds} beds · {Bathrooms} baths · Sleeps {Sleeps} · Vila Nova de Gaia`),
so the numbers have a single source of truth.

## Media

All image assets are placeholder SVGs written to `wwwroot/media/{guid}.svg` on dev startup; the
owner replaces them from the admin in Stage 7 (ADR 0006). Filenames use the asset's stable GUID.

- `home.hero.poster` — 1 image (poster).
- `home.gallery.1` … `home.gallery.10` — 10 gallery images, in collection **`home.gallery`**,
  `IsHero = true` on the first.
- `home.hero.video` — 1 video placeholder (no binary yet; poster points at the hero poster image).

## Content blocks

Kinds: `PlainText`, `RichText`, `ShortText`, `Url`, `ImageRef`, `VideoRef`, `Number`, `Boolean`.

### Section `home`

| Key | Kind | English value |
| --- | --- | --- |
| home.hero.headline | PlainText | Wake up to the Dom Luís I Bridge |
| home.hero.subheadline | PlainText | A two-bedroom apartment above the Douro — with the only true hot tub in the building. |
| home.hero.cta_text | ShortText | Check availability |
| home.hero.cta_href | Url | /book |
| home.hero.overlay_opacity | Number | 0.35 |
| home.hero.video | VideoRef | → media `home.hero.video` |
| home.hero.poster | ImageRef | → media `home.hero.poster` |
| home.snapshot.line | PlainText | 2 bedrooms · 4 beds · 2 baths · Sleeps 6 · Vila Nova de Gaia |
| home.view.title | PlainText | The view |
| home.view.body | RichText | The living room and both bedrooms open directly onto a generous balcony facing the Dom Luís I Bridge … Sunrises from this balcony are unforgettable. |
| home.hottub.title | PlainText | The hot tub |
| home.hottub.body | RichText | Step outside and sink into a genuine hot tub — currently the only one in the building … ready when you are. |
| home.layout.title | PlainText | The layout |
| home.layout.body | RichText | Inside, the kitchen and living area flow together as one continuous, light-filled space … a dedicated workspace with fast Wi-Fi. |
| home.highlights.1.title | ShortText | Guest Favorite |
| home.highlights.1.body | PlainText | Rated 4.91 out of 5 across 23 reviews |
| home.highlights.2.title | ShortText | Perfect ratings from families |
| home.highlights.2.body | PlainText | 100% of families rated it 5 stars in the past year |
| home.highlights.3.title | ShortText | Only true hot tub in the building |
| home.highlights.3.body | PlainText | Heated 27–33 °C, all year |
| home.reviews.aggregate.value | Number | 4.91 |
| home.reviews.aggregate.count | Number | 23 |
| home.reviews.subscores.cleanliness | Number | 4.9 |
| home.reviews.subscores.accuracy | Number | 4.9 |
| home.reviews.subscores.checkin | Number | 4.9 |
| home.reviews.subscores.communication | Number | 4.9 |
| home.reviews.subscores.location | Number | 4.6 |
| home.reviews.subscores.value | Number | 4.8 |
| home.location.blurb | RichText | Vila Nova de Gaia sits on the south bank of the Douro … exploring both banks of the Douro on foot. |
| home.host.name | ShortText | Home Me |
| home.host.years | Number | 11 |
| home.host.languages | ShortText | English, French, Portuguese, Spanish |

The long `*.body` / `blurb` paragraphs are stored verbatim in the seeder; see it for the full text.

### Section `rules`

| Key | Kind | English value |
| --- | --- | --- |
| rules.checkin_window | ShortText | 16:00 – 23:00 |
| rules.checkout_by | ShortText | 10:00 |
| rules.late_checkin_fee_eur | Number | 30 |
| rules.max_guests | Number | 6 |
| rules.smoking | PlainText | No smoking inside — balcony only. |
| rules.quiet_hours | PlainText | 23:00 – 08:00. No parties. |
| rules.children_note | PlainText | Not suitable for children and infants. |

### Section `footer`

| Key | Kind | English value |
| --- | --- | --- |
| footer.registration_label | ShortText | AL Registration |
| footer.registration_value | ShortText | 175890/AL |

### Section `faq`

`faq.1.q` … `faq.6.q` (ShortText) and `faq.1.a` … `faq.6.a` (RichText). **Questions and answers are
`TBD` placeholders** — the owner fills them via the admin in Stage 7.

### Section `amenities`

Each amenity is a `Boolean` block: `ValueText` is the label, `ValueBoolean` is availability.
**51 amenities** across 12 groups; the `not_available` group (4) has `ValueBoolean = false` and
renders greyed out. Keys are `amenities.<group>.<slug>`.

- **views** (2): City skyline view · River view
- **bathroom** (8): Bathtub · Hair dryer · Shampoo · Conditioner · Body soap · Bidet · Hot water · Shower gel
- **bedroom_laundry** (7): Washer · Hangers · Bed linens · Extra pillows and blankets · Room-darkening shades · Iron · Clothing storage
- **entertainment** (1): TV
- **climate** (2): Air conditioning · Heating
- **safety** (2): Fire extinguisher · First aid kit
- **internet_office** (2): Wifi · Dedicated workspace
- **kitchen** (16): Kitchen · Refrigerator · Microwave · Cooking basics · Dishes and silverware · Freezer · Dishwasher · Stove · Oven · Hot water kettle · Coffee maker · Wine glasses · Toaster · Baking sheet · Dining table · Coffee
- **outdoor** (3): Patio or balcony · Outdoor furniture · Outdoor dining area
- **parking** (3): Free parking on premises · Pool · Private hot tub (available all year, 24h)
- **services** (1): Host greets you
- **not_available** (4, `false`): Essentials (disposable plastic toiletries eliminated) · Smoke alarm · Carbon monoxide alarm · Private entrance

## Reviews

Six rows, all `Source = Airbnb`, `Rating = 5`, `IsPublished = true`. Surfaced (most recent first)
via `GET /api/reviews`.

| Guest | Location | StayedOn | Body |
| --- | --- | --- | --- |
| Aicha | Charlotte, North Carolina | 2026-08-01 | Good location, very easy to get into the center of Porto by using bus 901 or 906. Very convenient! The apartment was very modern with a beautiful view. The staff was very nice and even checked on my son when I mentioned he was sick and recommended a pharmacy near by that helped us out. |
| Mary | Bath, New York | 2026-09-10 | Beatrix was a very good communicator. She gave us good ideas and even walked us around the neighborhood when we were hungry after unpacking. She answered questions promptly. Thank you Beatrix! The hot tub and the views of Porto were relaxing. Washer/dryer and dishwasher made things easier. Local grocery store was a plus. Many choices of fine restaurants within easy Uber rides. Local seafood was fantastic. |
| Pascale | — | 2026-09-17 | There was a little mix-up with the address at the beginning. Ultra-secure building with 1 security guard 24/7. Amazing view. The host offered to book a taxi for us, but ultimately without success. |
| Raquel | — | 2026-08-01 | We had a very nice and quiet stay. The host was extremely attentive throughout the entire stay. When we return to Porto, we will certainly stay here again. |
| Emine | — | 2026-08-01 | Really lovely responsive host, the property was just like the photos and a lovely stay! |
| Patrice | — | 2026-08-01 | Pleasant stay, the view of Porto is beautiful. We had a great stay and were able to enjoy the private pool. The apartment is well-equipped. |

`StayedOn` dates are approximations of the source's relative timestamps ("August 2026",
"3 weeks ago", "2 weeks ago").
