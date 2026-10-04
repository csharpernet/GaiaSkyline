# 0015 — Admin rich-text editor (TipTap, self-hosted via esbuild)

- Status: Accepted
- Date: 2026-10-03
- Deciders: GaiaSkyline maintainers

## Context

Stage 7's content editor needs a WYSIWYG editor for `RichText` blocks (bold, italic, links, lists).
Constraints:

- **CSP forbids third-party scripts** (`script-src 'self' 'nonce-…'`; see `SecurityHeadersMiddleware`),
  so a CDN-hosted editor is out. Everything must be served from `'self'`.
- **MIT / open-source only.** No TipTap Pro or TipTap Cloud extensions (those are separate
  `@tiptap-pro/*` / collaboration packages behind a licence).
- The editor is an **admin-only** concern. It must never load on public pages, and the public
  Lighthouse budgets (`lighthouserc.json`, which only measures `/en`, `/en/gallery`, `/en/stories`,
  `/pt-pt`) must not change.
- `dotnet build` must produce the bundle with **no manual step**, in CI and in a clean clone.

TipTap is a headless, framework-agnostic ProseMirror wrapper. Its core, StarterKit and extension-link
packages are MIT. Unlike the other self-hosted vendor assets (web-vitals, MapLibre, Flatpickr), which
ship a prebuilt browser file we just copy (`copy-vendor.js`), TipTap is distributed only as ESM modules
that must be bundled into a single browser script.

## Decision

Bundle TipTap into one self-hosted IIFE with **esbuild**, driven by the existing MSBuild front-end
target.

- Pin (exact, no caret) in `package.json` devDependencies: `@tiptap/core`, `@tiptap/pm`,
  `@tiptap/starter-kit`, `@tiptap/extension-link` at **2.27.3** (the mature MIT 2.x line) and
  `esbuild` at **0.28.2**.
- The single entry `admin-src/editor.js` imports TipTap and exposes a tiny stable surface,
  `window.GaiaEditor.mount(el, { content, onUpdate })`. The editor is configured to match the
  server-side sanitizer allowlist (`HtmlContentSanitizer`): bold, italic, links, bullet/ordered lists,
  paragraphs — marks/nodes that would be stripped are disabled, so the WYSIWYG never lies.
- `npm run build:editor` bundles it to `wwwroot/js/admin/editor.bundle.js`
  (`--bundle --minify --format=iife --target=es2019`). `npm run build` now runs css + vendor + editor,
  and the MSBuild `FrontendBuild` target lists the bundle as an output and `admin-src/**/*.js` as an
  input, so `dotnet build` produces it incrementally. The first build runs `npm install` when
  `node_modules` is missing, so a clean clone and CI need no extra step.
- The compiled bundle is **git-ignored** (generated artifact), like `app.css` and the vendor copies.
- The bundle is referenced **only** from the content editor view (`Views/ContentAdmin/Edit.cshtml`),
  never from `_Layout` or any public view. ProseMirror ships no runtime stylesheet and we do not inject
  `<style>` (the CSP would block it), so the minimal `.ProseMirror` base CSS lives in `Styles/app.css`
  under admin-only selectors — a few hundred bytes that never apply on public pages.
- The hand-written glue (`wwwroot/js/content-admin.js`, served from `'self'`, no bundling) drives the
  language tabs, the toolbar and the media picker against `window.GaiaEditor`.

We chose **TipTap 2.x** over 3.x: 2.27.3 is the mature, widely documented MIT release and its API is
stable; 3.x reorganises packages and can be revisited later. We chose **esbuild** over Webpack/Rollup
for its zero-config speed and single-binary footprint; it resolves its platform binary through npm
optional dependencies recorded in `package-lock.json`, so the same build works on the Windows CI/dev
runners and on Linux App Service.

## Consequences

- **+** A real WYSIWYG editor, fully self-hosted and CSP-clean; no CDN, no Pro/Cloud dependency.
- **+** `dotnet build` yields the bundle with no manual step, in CI and a fresh clone.
- **+** Admin-only: public pages and their Lighthouse budgets are untouched.
- **−** Adds a bundler (esbuild) and the TipTap dependency tree to the front-end build. Pinned exactly
  and covered by `package-lock.json`; the vulnerable-package scan already runs in CI.
- The editor's capabilities and the server sanitizer must stay in step; both are intentionally narrow
  (bold, italic, links, lists) and documented together here.
