# 0016 — AVIF renditions via Magick.NET

- Status: Accepted
- Date: 2026-10-04
- Deciders: GaiaSkyline maintainers

## Context

ADR 0008 shipped the responsive raster pipeline (WebP + JPEG at 400/800/1600w + an LQIP) using
SixLabors.ImageSharp 2.1.x, and **deferred AVIF** because that line has no managed AVIF encoder.
The `<picture>` markup was left AVIF-ready (add an `image/avif` `<source>` first). Stage 7E picks
this up: AVIF is materially smaller than WebP/JPEG at the same quality, so lighting it up improves
LCP/transfer on the public pages at no layout cost.

Requirements:

- A real AVIF encoder that works **in dev (Windows), in CI (Windows), and on Linux App Service** with
  **no system install** (same constraint the Stage 7 video section states for its encoder).
- Produce `{stem}-{w}.avif` siblings next to the existing `{stem}-{w}.webp`/`.jpg`, for both the upload
  pipeline (`ImageRenditionService`) and the dev seeder (which writes the gradient placeholders), so the
  new `<source>` never points at a missing file.
- Keep ImageSharp 2.1.x (Apache-2.0) for the existing WebP/JPEG/LQIP work — no regression, no re-licensing.

Options considered:

- **ImageSharp 3.x** — still ships no AVIF encoder, and moves to the Six Labors Split License. Rejected.
- **An external `avifenc`/libaom binary invoked as a process** — needs per-OS binaries vendored and a
  process shell-out. More moving parts; rejected.
- **Magick.NET-Q8-AnyCPU** — the ImageMagick .NET binding. Its native package bundles libheif + aom, so
  AVIF encode works out of the box on Windows and Linux via NuGet, no system install. Apache-2.0.

## Decision

Add **Magick.NET-Q8-AnyCPU** (pinned `14.17.2`) for the AVIF rendition only; ImageSharp stays the
encoder for WebP/JPEG/LQIP.

- `AvifRaster` (Infrastructure/Media) centralises the encode: it takes already-encoded image bytes (a
  lossless PNG of the resized variant) and returns AVIF bytes at quality 50 (≈ the quality-72 WebP/JPEG
  variants, smaller). Both `ImageRenditionService` and `ContentSeeder` call it, so uploads and seeded
  placeholders produce byte-compatible AVIF.
- `ImageRenditionService` and the seeder now write `{stem}-{w}.avif` for all three widths; the seeder's
  idempotency fast-path also checks the AVIF master so a stale media folder regenerates it.
- `ResponsiveImage` builds the AVIF `srcset` and `_Picture` emits `<source type="image/avif">` **first**
  (before WebP, then JPEG), so capable browsers pick the smallest format and others fall back cleanly.
- Q8 (8 bits/channel) is enough for 8-bit web photography and keeps the encode fast and the package small.

## Consequences

- **+** Smaller hero/gallery images on capable browsers; the public `<picture>` is now fully AVIF→WebP→JPEG.
- **+** Cross-platform with no system install: the same build runs in dev, CI (Windows) and Linux App Service.
- **+** No change to the existing WebP/JPEG/LQIP path or its licence.
- **−** Adds the Magick.NET native dependency (bundled libs ~tens of MB) and a second imaging library.
  Scoped to the build/seed-time rendition step; never on the request path. Covered by the CI vulnerable-
  package scan.
- **−** AVIF encoding is CPU-heavier than WebP; it runs at upload/seed time (a Hangfire job in prod for
  uploads later), not per request, so it does not affect serving latency.
- Supersedes the "AVIF deferred" note in ADR 0008.
