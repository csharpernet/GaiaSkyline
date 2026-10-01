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

Seeded onto the `Property` entity (the Stage 1 entity has no capacity columns, so sleeps/beds/baths
live in content blocks — see `home.snapshot.line` and amenities).

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

> Not represented on the entity (captured in content instead): Sleeps 6 · Bedrooms 2 · Beds 4 ·
> Baths 2 · Bedroom 1: 2 single · Bedroom 2: 1 queen · Living room: 1 sofa bed.

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
Grounded in the provided property description. Groups with no source data in the brief
(`entertainment`, `climate`, `safety`, `parking`) are intentionally omitted until the real listing's
amenity inventory is imported.

| Key | Label | Available |
| --- | --- | --- |
| amenities.views.bridge_balcony | Balcony facing the Dom Luís I Bridge | true |
| amenities.views.river_panorama | Douro river & city panorama | true |
| amenities.outdoor.hot_tub | Private hot tub (27–33 °C) | true |
| amenities.outdoor.balcony | Private balcony | true |
| amenities.kitchen.fully_equipped | Fully equipped kitchen | true |
| amenities.bedroom_laundry.washing_machine | Washing machine | true |
| amenities.bedroom_laundry.iron | Iron | true |
| amenities.internet_office.wifi | Fast Wi-Fi | true |
| amenities.internet_office.workspace | Dedicated workspace | true |
| amenities.bathroom.two_bathrooms | 2 bathrooms | true |
| amenities.services.hot_tub_maintenance | Professionally maintained hot tub | true |
| amenities.not_available.children | Suitable for children and infants | false |
| amenities.not_available.smoking_indoors | Smoking indoors | false |

## Reviews

Six rows (`Source = Airbnb`, `IsPublished = true`): **Aicha, Mary, Pascale, Raquel, Emine,
Patrice**. The review **bodies, ratings and locations were not supplied in the Stage 2 brief**, so
they are placeholders (`Body = "TBD"`, `Rating = 5`, `GuestLocation = null`, staggered `StayedOn`
dates in 2025) to be replaced from the real listing via the admin (ADR 0007).
