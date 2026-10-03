# 0014 — Automatic nightly-price sync: a dormant provider seam

- Status: Accepted
- Date: 2026-10-03
- Deciders: GaiaSkyline maintainers

## Context

Stage 5 item 2 adds per-date nightly rates (`DailyRate`) that take precedence over the
season rules and base rate (ADR 0009). The property is managed by a company that runs
**PriceLabs** for dynamic pricing on top of **Hostify** (which is API-connected to
Airbnb). We would like the direct-booking site to follow those nightly prices
automatically instead of the owner retyping them.

We do **not** have verified API access or documentation for PriceLabs or Hostify, and
guessing endpoints/authentication is not acceptable — a wrong guess silently mis-prices
real bookings. So no real adapter can be written responsibly yet.

## Decision

Ship the **mechanism** now and leave the **adapter** for later:

- **`IRateProvider`** (Application) is the seam: `GetRatesAsync(from, to, ct)` returning
  `ProviderRate(Date, Price, MinNights?)`. No concrete `IRateProvider` is registered in
  the composition root, so the sync is dormant.
- **`FakeRateProvider`** (Infrastructure) returns injected rows — used by tests and for
  local experimentation only.
- **`RateSyncService`** (`IRateSyncService`) holds the import rules independent of any
  provider, so when a real adapter arrives the behaviour is already specified and tested:
  - next **18 months** only;
  - **skip** dates with `IsLockedByOwner` (owner overrides always win);
  - apply **`Pricing:DirectBookingAdjustmentPct`** to imported prices only (default `0`;
    e.g. `-10` for a 10% direct discount), **rounded to whole euros**;
  - **reject** any adjusted price outside **`Pricing:FloorPrice` / `Pricing:CeilingPrice`**,
    keeping the previous value and **emailing the owner**;
  - **upsert changed dates only**; a provider sync that changes nothing writes nothing;
  - any change invalidates the availability/quote/output caches (incl. the JSON-LD
    `priceRange`), via `IContentRevision.Bump()` + `IAvailabilityService.Invalidate()`.
- **Configuration** lives in the `Pricing` section (`PricingProviderOptions`):
  `Provider` (`None` | `PriceLabs` | `Hostify`, default `None`), `ApiKey`, `ListingId`,
  `DirectBookingAdjustmentPct`, `FloorPrice`, `CeilingPrice`, `SyncIntervalHours`
  (default 4). The API key is a credential and belongs in User Secrets / Key Vault; when
  a real adapter ships it must be **encrypted at rest** (DataProtection) exactly like the
  external-calendar URLs (Stage 5 item 1).
- **Scheduling**: a Hangfire recurring job `rate-sync` runs every `SyncIntervalHours`
  hours (`0 */N * * *`). With no provider configured it logs once at Information
  ("manual pricing mode") and exits — not a failure.
- **Health**: `PricingHealthCheck` reports *Manual pricing mode* (Healthy) when
  `Provider = None`; **Degraded** when a provider is configured but no rate has synced in
  the last 24 hours; Healthy otherwise. Staleness is derived from the newest
  `SourceUpdatedAtUtc` across provider-sourced `DailyRate` rows, so it needs no extra
  state table. A sync that fails to reach the provider also emails the owner.

### To add a real adapter later

1. Confirm the real endpoints, auth and rate shape against **official** PriceLabs/Hostify
   docs (not guesses).
2. Implement `IRateProvider` in Infrastructure (wrap the HTTP call in the existing Polly
   pipeline pattern — retry + circuit breaker + timeout — as the calendar importer does).
3. Register it and store the key encrypted; set `Pricing:Provider`. The job, rules,
   health check and tests already exist — nothing else changes.

## Consequences

- **+** The behaviour the owner depends on (lock wins, floor/ceiling guard, direct-booking
  adjustment, cache invalidation) is implemented and unit-tested **before** any real API
  is touched, so wiring an adapter is low-risk.
- **+** No invented endpoints; the site cannot mis-price from a wrong guess.
- **−** Until an adapter ships, nightly prices are entered **manually** (bulk set, clear
  range, lock, or CSV import — see the runbook). This is expected and is the same posture
  as the external calendars (Stage 5 item 1).
- **−** `[DisableConcurrentExecution]` is **not** applied to the job: the attribute lives
  in Hangfire, which the Application/Infrastructure layers do not reference (clean
  architecture). Overlap is harmless here — the sync is idempotent (upsert changed only)
  and the 4-hour cadence far exceeds a run — so this is deferred rather than worked around
  with a wrapper; revisit if a real adapter makes a run long enough to overlap.
