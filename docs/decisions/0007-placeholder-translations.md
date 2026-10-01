# 0007 — Placeholder translations and placeholder content

- Status: Accepted
- Date: 2026-10-01
- Deciders: GaiaSkyline maintainers

## Context

The site is multilingual (en, pt-PT, es, fr, de) but we do not have real translations yet, and some
source content (FAQ answers, the six review bodies) was not available for this stage. We still want
localization to be visibly working and the seeder to produce a complete, demonstrable dataset
before real copy exists.

## Decision

Seed **placeholder translations and placeholder content**, clearly marked, to be replaced by the
owner via the admin in Stage 7:

- **Text blocks** are seeded in English and in the other four languages as the English value
  prefixed with `[PT] `, `[ES] `, `[FR] `, `[DE] ` — so switching language visibly changes the copy.
- **Non-text blocks** (Url, Number, media refs) are seeded in **English only**, so requests for
  other languages fall back to English. This also gives us a built-in fallback test fixture.
- **FAQ** questions and answers are seeded as `TBD` — the real FAQ text was not provided, so the
  owner fills it via the admin in Stage 7.
- **Reviews** (Aicha, Mary, Pascale, Raquel, Emine, Patrice) are seeded with their **real verbatim
  bodies, ratings and locations** (`Source = Airbnb`, `IsPublished = true`), surfaced via
  `GET /api/reviews`.
- **Amenities** are seeded as the **full source list** (51 across 12 groups); the `not_available`
  group carries `ValueBoolean = false`.
- **Property capacity** (Sleeps/Bedrooms/Beds/Bathrooms/BedsBreakdown) lives on the `Property`
  entity (migration 0003); `home.snapshot.line` derives its numbers from it.

## Consequences

- **+** Localization, fallback (`‹key›` when wholly missing) and the full content pipeline are
  demonstrable and testable today.
- **+** Gaps are obvious: `[DE] …` prefixes and `TBD` values read as "not real yet".
- **−** The seeded dataset is not production copy. Real translations and the real FAQ text must be
  entered in Stage 7 before go-live (reviews and amenities are now real).
- See `docs/content-seed.md` for exactly what is placeholder vs. authoritative.
