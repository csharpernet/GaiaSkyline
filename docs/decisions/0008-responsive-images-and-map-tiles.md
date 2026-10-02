# 0008 — Responsive image pipeline and lazy map tiles

- Status: Accepted
- Date: 2026-10-02
- Deciders: GaiaSkyline maintainers

## Context

Stage 3 commits to strict Core Web Vitals (LCP < 2.0s, CLS < 0.05). That needs modern responsive
images (right-sized, next-gen formats, no layout shift) and a location map that does not tax the
initial load. Real photography and a production tile strategy do not arrive until Stage 7, but the
markup, SEO and performance behaviour must be exercised now with placeholder assets.

## Decision

### Responsive rasters (`<picture>` + srcset)

- The dev seeder uses **ImageSharp 2.1.x** (Apache-2.0; 3.x moved to the Six Labors Split License)
  to generate deterministic placeholder rasters: **WebP + JPEG at 400 / 800 / 1600 w**, plus a
  24px-wide **LQIP** stored inline on `MediaAsset.Lqip` as a `data:` URI.
- `MediaAsset.BlobUri` points at the full-width JPEG (`/media/{slug}-1600.jpg`) — a real, directly
  usable file and a safe Open Graph image. The shared `_Picture` partial derives the WebP/JPEG
  `srcset` from that stem.
- **No layout shift:** the wrapper carries an aspect-ratio utility and the image fills it absolutely,
  with `width`/`height` attributes retained for the intrinsic ratio. The LQIP sits behind as a
  blurred `<img>` (CSP forbids inline `style`, so no `background-image` data URI).
- **AVIF is deferred.** There is no managed AVIF encoder available here. The `<picture>` structure is
  AVIF-ready (add an `image/avif` `<source>` first); real AVIF renditions come from the Stage 7
  upload pipeline alongside real photography.

### Lazy, self-hosted map

- **MapLibre GL** (BSD-3-Clause) is self-hosted (vendored from npm at build time, served from our
  origin — never a CDN) so no third-party script/style is in the critical path.
- The library (~800 KB) and its stylesheet are **not loaded until the map scrolls near the viewport**
  (IntersectionObserver injects them on demand), so the map never affects LCP/INP on first load.
- The marker is deliberately **coarse**: coordinates are rounded to 3 decimals (~110 m) and shown as a
  translucent area circle, never the exact address (which is shared after booking).
- **Tiles** come from `tile.openstreetmap.org` for now, with attribution. The CSP whitelists only
  that host, and only on the directives that need it (`img-src`, `connect-src`) plus `worker-src
  blob:` for MapLibre's renderer worker.

## Consequences

- **+** Real responsive-image behaviour (format negotiation, srcset selection, LQIP, zero CLS) is
  testable in dev with zero cloud dependencies.
- **+** The map costs nothing until the user scrolls to it; the rest of the page stays within budget.
- **−** The CSP carries a narrow external allowance (the OSM tile host, `blob:` worker). **Production
  should proxy or self-host tiles** and drop the external allowance — tracked for Stage 7.
- **−** Placeholder rasters are generated gradients, not real cartography/photography; both are
  replaced by the Stage 7 upload pipeline. See [0006](0006-media-storage-local-disk-dev-blob-prod.md).
