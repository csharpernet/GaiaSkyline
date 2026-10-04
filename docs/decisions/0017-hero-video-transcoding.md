# 0017 — Hero background video: FFmpeg now, Mux in production

- Status: Accepted
- Date: 2026-10-04
- Deciders: GaiaSkyline maintainers

## Context

The home hero is a muted, looping background video built for both desktop (landscape 16:9) and
mobile (portrait 9:16), in three codecs each (AV1, VP9, H.264) so every browser gets an efficient
rendition, plus landscape/portrait posters that are the LCP element (Stage 7E-4). The owner uploads
a single source clip (up to 500 MB / 60 s) and the system must trim it to a 10–20 s seamless loop,
strip the audio, crop the mobile renditions around an owner-chosen focal point, generate the posters
through the existing image pipeline, and swap the new video in atomically only once every rendition
is ready — all without blocking the request.

We need a transcoder that works locally on Windows with zero cloud dependencies, and a production
path that does not pin the web app's CPU for minutes per upload.

## Decision

Model transcoding behind an **`IVideoTranscoder`** abstraction (Application layer) and run it as a
**Hangfire background job**, so the implementation is swappable and the upload request returns
immediately.

- **Dev / self-hosted:** `FfmpegVideoTranscoder` shells out to **FFmpeg** (`winget install Gyan.FFmpeg`;
  encoders `libsvtav1` for AV1, `libvpx-vp9` for VP9, `libx264` for H.264). It probes the source with
  `ffprobe`, trims/crops/scales per rendition, strips audio (`-an`), writes constant-frame-rate output
  with a keyframe at least every 2 s, adds `+faststart` to the MP4s, and extracts the poster frames
  (fed to `ImageRenditionService` for the AVIF/WebP/JPEG + LQIP set). FFmpeg is **not** bundled; the
  path is configured (`VideoTranscoding:FfmpegPath`) and the transcoder no-ops with a clear error where
  FFmpeg is absent.
- **Production (recommended): [Mux](https://mux.com).** Mux is a managed video API: you upload the
  source and it produces adaptive renditions and thumbnails. A future `MuxVideoTranscoder : IVideoTranscoder`
  uploads the trimmed source and maps Mux outputs onto our rendition slots. **Closest-equivalent note:**
  Mux's default output is adaptive HLS rather than six discrete AV1/VP9/H.264 files; the adapter will
  either request Mux's static MP4 renditions (desktop + mobile) and keep the AV1/VP9 WebM variants as a
  self-hosted FFmpeg fallback, or serve Mux's HLS with the same poster-LCP behaviour. Either way the
  public markup (`<video>` with typed `<source>`s) and the `HeroVideo` model are unchanged — only the
  transcoder implementation and where the bytes live differ. The interface is kept deliberately
  high-level (`ProduceHeroRenditionsAsync(request) -> rendition set`) precisely so a managed service can
  satisfy it.

The owner-facing state lives in a dedicated **`HeroVideo`** aggregate (not the general media library):
it carries the six renditions, both posters, the trim/focal/crossfade settings, the transcode status,
and an `IsLive` flag. The previous live video stays `IsLive` until the new one reaches `Ready`, at which
point a single transaction promotes the new one and retires the old (**atomic swap**).

## Consequences

- **+** The full hero pipeline (trim, crop, multi-codec, posters, swap) works locally with only a
  local FFmpeg install; nothing cloud is required to develop or test it.
- **+** Production can offload transcoding to Mux without touching the domain model, the job, or the
  public markup — just a new `IVideoTranscoder` and configuration.
- **+** Uploads never block: the request stores the source and enqueues the job; the old video keeps
  playing until the new renditions are fully ready.
- **−** FFmpeg is an external process with its own version/encoder surface; CI must install it to run
  the rendition integration tests (added in 7E-4c, probed with `ffprobe`; skipped with a clear reason
  where FFmpeg is absent).
- **−** Mux (or any managed option) is a paid third-party dependency and a data-residency consideration;
  it is a recommendation to evaluate at launch, not a committed integration. Until then the FFmpeg
  transcoder is the only implementation and runs on the App Service host.
