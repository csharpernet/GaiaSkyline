# 0024 — Blob media storage via a publish seam

Date: 2026-10-06 · Status: accepted · Stage 8 Part B

## Context

Media renditions (9 raster files per image; 6 video renditions + 2 posters per hero) are generated as
stem-named files (`{stem}-{w}.{ext}`) into a directory and served from `/media`. In development that
directory is `wwwroot/media` served by static files. In production the spec requires the bytes to live in
an Azure Blob container (durable, zone-redundant, soft-deleted, cached by Front Door) — not on the App
Service file system. The single-file `IMediaStorage` abstraction (GUID-named uploads) does not model the
multi-file, specific-stem rendition set.

## Decision

- A new `IMediaFileStore` models the directory-of-stem-named-files lifecycle: **publish** all of a stem's
  files after generation, **move** a stem (rename), **open** one file (serving), and whether the store
  **serves** media itself.
- **Local disk** (dev / CI / local-only mode): the working directory IS `wwwroot/media`, so publish is a
  no-op, move renames in place, and static files serve the bytes — **byte-identical to before**.
- **Azure Blob** (deployed production): the pipeline generates into a temp working directory, `PublishStem`
  uploads the stem's files to the `media` container (immutable cache header — a replace keeps the filename
  and bumps the `?v=` token, ADR 0019-era versioning), `MoveStem` renames server-side, and a `/media`
  endpoint streams files back with Front Door caching in front. Reached via the managed identity.
- Only the **admin** media paths (`AdminMediaService` upload/replace/rename and the hero transcode job)
  publish through the store. The dev **seeder is disabled in production** (`SeedContentOnStartup=false`), so
  it never needs Blob; initial production media comes from admin uploads or the one-off migration command
  (`--migrate-media`).

## Consequences

- Development, CI and local-only mode are unchanged — the local store's publish/move operate on the same
  directory the pipeline already wrote to, so every existing test passes without modification.
- The Blob path is unit-tested for its call contract with a recording fake; its end-to-end behaviour against
  real Blob (serving, migration, hero upload) is a DEPLOYMENT PHASE verification, since it depends on the
  Front-Door→origin→Blob model that only exists once deployed.
- `IMediaStorage` (single files, e.g. generated guest PDFs) gets a parallel `BlobMediaStorage` bound in the
  same deployed-only switch.
