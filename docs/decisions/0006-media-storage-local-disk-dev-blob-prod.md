# 0006 — Media storage: local disk in dev, Azure Blob in prod

- Status: Accepted
- Date: 2026-10-01
- Deciders: GaiaSkyline maintainers

## Context

`MediaAsset.BlobUri` points at the bytes for an image or video. In production these live in Azure
Blob Storage. We do not have Blob wired up in this stage, and uploads (admin) only arrive in
Stage 7. We still want the content model, seed data and read APIs to work locally now.

## Decision

Model storage behind an **`IMediaStorage`** abstraction (declared in the Application layer now;
implemented with the upload flow in Stage 7).

- **Dev:** media bytes live on local disk under `wwwroot/media/`, served as static files.
  `BlobUri` is a **relative path** (`/media/{guid}.{ext}`). The seeder writes placeholder SVGs there
  on Development startup.
- **Prod:** an Azure Blob implementation of `IMediaStorage` stores bytes and `BlobUri` becomes the
  blob URL. Nothing above the storage seam changes.

`MediaAsset` stays storage-agnostic: it just holds a `BlobUri` string. Resolution and the read APIs
never assume a particular backend.

## Consequences

- **+** Full content/media flow works locally with zero cloud dependencies.
- **+** Swapping to Blob in prod is a single implementation of `IMediaStorage` plus configuration;
  no model or API changes.
- **−** Relative-vs-absolute `BlobUri` differs by environment; the front-end must treat `BlobUri` as
  opaque and not assume it is always relative.
- The seeded placeholder binaries are throwaway SVGs; the owner uploads the real photos/video in
  Stage 7.
