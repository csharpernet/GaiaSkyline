// Copies self-hosted third-party assets from node_modules into wwwroot so they are served from
// 'self' (CSP-friendly, no third-party in the critical path). Run via `npm run build:vendor`.
const fs = require('fs');
const path = require('path');

const jsDir = path.join('wwwroot', 'js', 'vendor');
const cssDir = path.join('wwwroot', 'css', 'vendor');
fs.mkdirSync(jsDir, { recursive: true });
fs.mkdirSync(cssDir, { recursive: true });

const copies = [
  ['node_modules/web-vitals/dist/web-vitals.iife.js', path.join(jsDir, 'web-vitals.iife.js')],
  // MapLibre GL powers the (lazy, below-the-fold) location map. Self-hosted so no third-party
  // script/style is in the critical path; the tile source is configured per environment.
  ['node_modules/maplibre-gl/dist/maplibre-gl.js', path.join(jsDir, 'maplibre-gl.js')],
  ['node_modules/maplibre-gl/dist/maplibre-gl.css', path.join(cssDir, 'maplibre-gl.css')],
];

for (const [src, dest] of copies) {
  fs.copyFileSync(src, dest);
  console.log('copied ' + dest);
}
