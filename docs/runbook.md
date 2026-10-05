# Runbook

Operational procedures for GaiaSkyline. (Stage 5 adds the Airbnb calendar-sync procedures here.)

## Testing the booking + payment flow locally

1. **Email sink** — run smtp4dev so booking emails are captured:
   ```bash
   smtp4dev        # SMTP localhost:2525, web UI http://localhost:5000
   ```
2. **Stripe keys** — in User Secrets for `src/GaiaSkyline.Web` (test keys only; never commit):
   ```bash
   dotnet user-secrets set "Stripe:PublishableKey" "pk_test_…"
   dotnet user-secrets set "Stripe:SecretKey"      "sk_test_…"
   ```
3. **Webhook forwarding** — the webhook is the source of truth for confirmation, so forward events
   with the Stripe CLI and set the signing secret it prints:
   ```bash
   stripe login
   stripe listen --forward-to https://localhost:7443/webhooks/stripe
   dotnet user-secrets set "Stripe:WebhookSecret" "whsec_…"
   ```
4. **Run the app**, open `/en/book`, pick dates + guests, Reserve, fill the checkout form, and pay with a
   [Stripe test card](https://stripe.com/docs/testing):
   - Success: `4242 4242 4242 4242`
   - 3DS authentication: `4000 0025 0000 3155`
   - Declined: `4000 0000 0000 0002`
   Any future expiry, any CVC, any postal code. On success you land on the confirmation page and a
   confirmation email appears in smtp4dev (in the guest's language); the owner notification goes to
   `nuno_senica@hotmail.com` in English.

### Multibanco (manual)

Multibanco only appears when check-in is ≥ 10 days away. To test end to end in Stripe test mode:
1. On `/book`, choose dates at least 10 days out and proceed to checkout.
2. In the Payment Element choose **Multibanco** and confirm. Stripe returns a test Entity + Reference and
   the booking stays *AwaitingPayment* with the voucher shown on the confirmation page.
3. Simulate payment from the Stripe CLI to drive confirmation:
   ```bash
   stripe trigger payment_intent.succeeded
   ```
   (or pay the test voucher via Stripe's hosted page). The webhook then confirms the booking; the
   confirmation page updates on its 30-second poll and the Multibanco-reference email is in smtp4dev.

## Running the Playwright booking E2E

The `/book` page checks run in CI's browser-quality job. The Stripe payment flows are gated:
```bash
cd tests/e2e
BASE_URL=https://localhost:7443 STRIPE_E2E=1 npx playwright test booking.spec.js
```
To run them in CI, add `Stripe:PublishableKey`/`SecretKey`/`WebhookSecret` as repository secrets and set
`STRIPE_E2E=1` for the browser-quality job.

## Calendar sync (Stage 5)

The property is managed by a company using **Hostify** (API-connected to Airbnb). Until an
iCal URL is available, sync is **manual, in both directions**.

### Manual procedure (now)
- **A direct booking comes in** (or is cancelled): the owner notification email leads with
  "Action needed: ask the management company to block/unblock <dates> in Hostify". If
  `PropertyManager:NotificationEmails` is set, the management company is emailed directly
  (with an .ics). Forward/act on it so Hostify blocks those nights.
- **Hostify/Airbnb has a reservation we must not double-book**: the owner enters an
  `OwnerBlock` of kind **ExternalBooking** for those dates (admin UI in Stage 7; API:
  `POST /api/admin/owner-blocks`). These block our calendar but are **not** exported.
- **Owner holds dates** (maintenance, personal use): enter an `OwnerBlock` of kind
  **OwnerUnavailable** — these ARE exported in our .ics.

### What to ask the management company
- A **read-only iCal export URL** for this listing (Hostify can provide one). It is a
  credential — share it securely.

### Enabling iCal import once you have the URL (no code change)
Add it to User Secrets (or Key Vault) and restart:
```bash
dotnet user-secrets set "ExternalCalendars:Sources:0:Name" "Hostify"
dotnet user-secrets set "ExternalCalendars:Sources:0:IcsUrl" "https://…ics"
dotnet user-secrets set "ExternalCalendars:Sources:0:IsEnabled" "true"
```
The import job (every 15 min) then populates external blocks automatically; the health
check flips from "Manual mode" to reporting sync status. Replace the placeholder fixture
in the tests with a real Hostify export to lock in parsing.

### Cleaning up manual duplicates after switching to iCal
Once import is live, `GET /api/admin/owner-blocks/duplicates` lists ExternalBooking owner
blocks whose dates exactly match an imported block — delete those manual copies (they're
now maintained by the feed).

### Our calendar export
`GET /calendar/{token}/gaia-skyline.ics` (token from `Ics:ExportToken`) exports active
bookings + OwnerUnavailable blocks (never ExternalBooking blocks, to avoid echo). Give
this URL to the management company so Hostify can import our direct bookings.

## Nightly prices (Stage 5 item 2)

Like the calendar, prices are **owner-managed (manual)** until a pricing provider API is
available. The management company runs **PriceLabs** (dynamic pricing) on top of Hostify,
but we have no verified API access yet, so there is **no automatic adapter** — see
[ADR 0014](decisions/0014-automatic-pricing-provider-dormant.md). A per-date rate
(`DailyRate`) takes precedence over the season rules and the base rate (ADR 0009); if a
date has no daily rate it falls back to the season, then the base.

### Setting prices manually (now)
All endpoints are Owner-only and audited. Dates are inclusive.
- **Set a range**: `PUT /api/admin/pricing/rates` `{ "from": "2026-07-01", "to":
  "2026-08-31", "nightlyRate": 140, "minNights": 3 }`.
- **Clear a range** (fall back to season/base): `DELETE /api/admin/pricing/rates?from=…&to=…`.
- **Lock/unlock** (locked dates are never overwritten by a future import):
  `POST /api/admin/pricing/rates/lock` `{ "from": …, "to": …, "locked": true }`.
- **CSV import**: `POST /api/admin/pricing/rates/csv` `{ "csv": "date,price,min_nights\n2026-07-01,140,3" }`.
  Default is a **dry-run** that returns the parsed rows (`set` / `invalid: …`) without
  saving; add `?apply=true` to commit the valid rows. CSV columns: `date,price[,min_nights]`;
  a `date,…` header row is ignored.

Mirror the owner's Airbnb/PriceLabs nightly prices here. Any change takes effect on the
next quote and invalidates the availability/quote/output caches (including the JSON-LD
"from €X"). An existing booking keeps the price it was quoted — changing a daily rate never
re-prices a confirmed stay.

### What to ask the management company
- Whether **PriceLabs or Hostify exposes a rates API** for this listing, plus official
  docs and an API key. Until then, pricing stays manual.
- A sensible **direct-booking discount** to pass on (we take no platform fee): decide a
  percentage for `Pricing:DirectBookingAdjustmentPct` to apply to imported prices later.

### Enabling automatic sync once an adapter exists (future)
No adapter ships today. When one is built against official docs (implement `IRateProvider`
— see ADR 0014), configure and restart:
```bash
dotnet user-secrets set "Pricing:Provider" "PriceLabs"   # or Hostify
dotnet user-secrets set "Pricing:ApiKey" "…"             # credential — User Secrets/Key Vault only
dotnet user-secrets set "Pricing:ListingId" "…"
dotnet user-secrets set "Pricing:DirectBookingAdjustmentPct" "-10"   # optional, whole-euro rounding
dotnet user-secrets set "Pricing:FloorPrice" "40"        # reject imports below this
dotnet user-secrets set "Pricing:CeilingPrice" "500"     # reject imports above this
```
The `rate-sync` Hangfire job (every `Pricing:SyncIntervalHours`, default 4) then imports
the next 18 months: it **skips owner-locked dates**, applies the direct-booking adjustment
(rounded to whole euros), **rejects** prices outside the floor/ceiling (keeps the previous
value and emails the owner), and upserts only changed dates. With no provider configured it
logs "manual pricing mode" and the health check reports Healthy ("Manual pricing mode");
once configured, the check goes **Degraded** if no rate has synced in 24 hours.

## Accounts & auth (Stage 6)

- **Owner**: seeded in Development from `Owner:Email` + `Owner:Password` (User Secrets).
  In production: `dotnet run -- create-owner --email you@example.com --password ****`
  (password may instead be the `Owner:Password` secret). First login at `/admin/login`
  forces TOTP enrolment (scan the QR, save the 10 recovery codes).
- **Owner IP allowlist**: set `Owner:AllowedIps` (array) to restrict `/admin`; empty =
  no restriction (the Development default).
- **Partner JWT**: set `Jwt:SigningKey` (≥ 32 chars) in User Secrets for stable tokens.
  `POST /api/partner/token` → access + refresh; `POST /api/partner/token/refresh`;
  `GET /api/partner/me` with `Authorization: Bearer`.

### Automated auth E2E (Playwright) — Stage 7 item 0

The owner TOTP login and the guest magic-link → booking → cancel flows are now automated
(`tests/e2e/tests/admin-auth.spec.js`, `guest-magic-link.spec.js`). They run in CI's
browser-quality job and reuse `helpers/admin.js` → `loginAsOwner(page)`, which every later
admin E2E builds on.

They depend on the **E2E seam**, which is OFF by default and must never run in Production
(`Program.cs` throws if `E2E:Enabled` is set in Production). When enabled it: captures
outbound email in memory (read at `GET /test/emails`), seeds the Owner with a known
authenticator key + 2FA (so the test computes a valid TOTP, see `helpers/totp.js`), and
seeds a known `AwaitingPayment` booking, reset on each startup. To run locally:
```bash
# terminal 1 — app with the seam on (ephemeral test Owner on a throwaway DB)
E2E__Enabled=true Owner__Email=owner.e2e@gaiaskyline.test Owner__Password='<12+ chars>' \
  Features__SeedContentOnStartup=true ASPNETCORE_ENVIRONMENT=Development \
  dotnet run --project src/GaiaSkyline.Web --urls http://localhost:5080
# terminal 2 — the specs (OWNER_PASSWORD must match)
cd tests/e2e
E2E_SEAM=1 OWNER_EMAIL=owner.e2e@gaiaskyline.test OWNER_PASSWORD='<same>' \
  npx playwright test admin-auth guest-magic-link
```
The magic-link test cancels the seeded booking; restart the app to run it again. In CI the
Owner password is a per-run generated value (never committed); the authenticator key is a
non-secret test fixture, useless without that password.

### Lighthouse budgets

Core Web Vitals budgets are enforced in the same browser-quality job via `lighthouserc.json`
(error-level: LCP ≤ 2000 ms, CLS ≤ 0.05, FCP ≤ 1500 ms, server-response ≤ 400 ms; SEO /
performance / a11y / best-practices scored). Admin pages are noindex and not in the Lighthouse
URL set, so the admin UI never affects the public budgets.

## Unpaid-hold expiry

Card/wallet holds are released 30 minutes after creation; Multibanco holds at their voucher expiry. A
Hangfire job (`unpaid-hold-expiry`, every 5 min) is the safety net if a webhook is missed. Force a run
from the Hangfire dashboard (mapped with the admin area in Stage 7).

## Local LocalDB health (dev-box only)

Two local failure modes look like app regressions but are environmental — check these before
debugging the app:

1. **Leftover test databases.** Aborted test runs and manual E2E runs can leave dozens of
   `GaiaSkyline_E2E_*/GaiaSkyline_Web_*/GaiaSkyline_Tests_*` databases attached with `AUTO_CLOSE`
   on; every touch then pays a database open/recovery and the whole instance degrades. Fix:
   `powershell -File tools/cleanup-test-dbs.ps1 -KillStrayProcesses`. The final step of
   `tools/run-ci-gates.ps1` fails if any stray is left behind, so leaks surface immediately.
2. **The 25-second named-pipe stall.** On some dev boxes, cold public-page renders stall in quanta
   of ~25 s (25/50/95/125 s) inside the content-blocks query while SQL sits completely idle — the
   client's async named-pipe read misses its completion and is nudged by a ~25 s timer. It is a
   box-level LocalDB/named-pipe pathology (reproduced with and without MARS and TLS): CI
   (windows-latest LocalDB) and production (TCP to Azure SQL) never see it. Signature check: EF
   command logging shows `Executed DbCommand (25,0xx ms)` while `sys.dm_exec_requests` is empty.
   Remediation: reboot the box; if it persists, point local dev at SQL Server Express over TCP.
   Don't chase it as an app bug, and don't trust local cold-render timings on an affected box —
   the CI Lighthouse `server-response-time` budget is the real gate.

`MultipleActiveResultSets` was removed from every connection string in Stage 7 (2026-10-05): the app
never needs MARS (EF buffers result sets), and it is one more moving part in the named-pipe stack.
Don't reintroduce it.
