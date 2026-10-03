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

### Manual browser auth tests

The xUnit suite covers TOTP verify + recovery codes, lockout, the IP allowlist, magic
links and JWT rotation/reuse at the service level. To exercise the full browser flows:
1. **Owner TOTP**: seed an Owner, open `/admin/login`, sign in, scan the QR into an
   authenticator app, enter the code, confirm you reach `/admin`; sign out and confirm a
   second login requires the code.
2. **Guest magic link**: book a stay, then at `/en/account/magic-link` enter the
   reference + email; open the link from smtp4dev and confirm `/en/my/booking/{ref}`
   loads and that the link fails on a second use.

## Unpaid-hold expiry

Card/wallet holds are released 30 minutes after creation; Multibanco holds at their voucher expiry. A
Hangfire job (`unpaid-hold-expiry`, every 5 min) is the safety net if a webhook is missed. Force a run
from the Hangfire dashboard (mapped with the admin area in Stage 7).
