# 0018 — Same-origin proxy for map tiles

- Status: Accepted
- Date: 2026-10-05
- Deciders: GaiaSkyline maintainers

## Context

[0008](0008-responsive-images-and-map-tiles.md) shipped the lazy MapLibre location map with raster
tiles fetched directly from `tile.openstreetmap.org`, and the Content-Security-Policy carried a
narrow external allowance for that host (`img-src`, `connect-src`). That ADR explicitly deferred a
production tile strategy to Stage 7: "Production should proxy or self-host tiles and drop the external
allowance."

Stage 7 §5 closes that out. The goals: remove the only remaining third-party host from the CSP (so
the public pages are same-origin for scripts, styles, images and fetches, except the Stripe
allowances scoped to the checkout pages), and keep the map interactive without taking on a large
build-time asset.

## Decision

- Add a **same-origin tile proxy**: `GET /map/tiles/{z}/{x}/{y}.png` (`MapTilesController`) fetches the
  tile from OSM **server-side** and streams it back. The browser only ever talks to our origin.
- The map's `data-tiles` template becomes `/map/tiles/{z}/{x}/{y}.png`; OSM **attribution is still
  shown** on the map.
- The CSP **drops** `https://tile.openstreetmap.org` from `img-src` and `connect-src`; `'self'` now
  covers the tiles. `worker-src 'self' blob:` stays (MapLibre's renderer worker).
- **OSM tile usage policy compliance:** the server-side `HttpClient` sends a descriptive
  `User-Agent` identifying the app (the policy forbids anonymous/browser-spoofed agents), and
  responses are cached hard — `[OutputCache]` server-side plus `Cache-Control: public,
  max-age=604800, immutable` to the browser. The site shows one fixed location, so only a small,
  bounded set of tiles is ever requested.
- The proxy **range-checks** `z/x/y` (0 ≤ z ≤ 19; x, y within `2^z`) and only ever requests a real
  tile path, so it is not an open forward-proxy / SSRF surface. Upstream failures return `502` and are
  not cached; the map fails silently client-side (the fallback text remains).

### Alternative considered — self-hosting a fixed tileset

Because the map shows a single location, we could pre-download the handful of tiles for the supported
zoom levels and serve them as static files (zero runtime dependency on OSM). Rejected for now: it adds
a build/fetch step and freezes the cartography, whereas the proxy keeps tiles fresh, keeps full
pan/zoom, and still removes the external CSP allowance. The proxy can be swapped for a static tileset
later without touching the client or the CSP.

## Consequences

- **+** No third-party host in the public CSP's `img-src`/`connect-src`; the external-allowance debt
  from [0008](0008-responsive-images-and-map-tiles.md) is cleared.
- **+** The map stays fully interactive; the client change is a single `data-tiles` URL.
- **+** Hard caching (server + browser) keeps OSM traffic minimal and well within the usage policy.
- **−** Tiles are now fetched by our server rather than the browser (a small amount of egress + a
  cache in memory). Acceptable for a single-location, low-traffic site; a CDN/static tileset is the
  next step if traffic grows.
