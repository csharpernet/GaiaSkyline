# 0013 — Identity, authentication and authorization (Stage 6)

## Status
Accepted.

## Context
Stage 6 adds accounts and three roles (Owner, Partner, Guest), Owner 2FA, guest
magic-link access, a partner JWT API, and the Owner content/media write API.

## Decisions
- **ASP.NET Core Identity on `AppDbContext`**, keyed by `Guid` to match the domain's
  strongly-typed ids. EF + Identity packages are pinned to 10.0.12 to match the Web
  host's resolved model (a 10.0.1/10.0.12 skew produced a `PendingModelChangesWarning`).
- **Password policy**: length ≥ 12 as the primary gate plus a Have I Been Pwned
  k-anonymity check that **fails open** (an HIBP outage never blocks sign-up). Lockout
  after 5 attempts for 15 minutes.
- **Cookies**: HttpOnly, Secure, SameSite=Lax. Guest/Partner sliding 14 days; Owner
  an 8-hour absolute session. `/api` paths answer 401/403 rather than redirecting.
- **Owner**: no self-registration (seeded in Dev; `create-owner` CLI in prod), role +
  IP allowlist (`Owner:AllowedIps`, empty = no restriction) + forced TOTP enrolment;
  "remember this browser" is never offered.
- **Guest magic link**: data-protection-signed token, single use + 30-minute expiry
  tracked in the DB; enumeration-safe issuing; a short booking-scoped cookie grants
  access after consumption so no account is required.
- **Partner JWT**: 15-minute HS256 access tokens; 7-day refresh tokens stored as
  SHA-256 hashes, rotated on use, with family revocation on reuse. Signing key from
  User Secrets; an ephemeral per-process key is used when unset so the app still boots.
- **Owner write API** is cookie-authenticated; SameSite=Lax covers CSRF for the
  state-changing POST/PUT endpoints. Every write bumps `IContentRevision`
  (invalidating the output + content caches) and writes an `AuditEvent`.

## Consequences
- Secrets (`Owner:Email`/`Password`, `Jwt:SigningKey`, HIBP needs no key) live in User
  Secrets / Key Vault; the app boots without them (degraded: ephemeral JWT key).
- The browser auth flows (owner TOTP login, guest magic link) are covered by the xUnit
  suite at the service/manager level and documented for manual browser testing; a full
  Playwright pass is a follow-up (see runbook).
