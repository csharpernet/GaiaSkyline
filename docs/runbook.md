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

## Unpaid-hold expiry

Card/wallet holds are released 30 minutes after creation; Multibanco holds at their voucher expiry. A
Hangfire job (`unpaid-hold-expiry`, every 5 min) is the safety net if a webhook is missed. Force a run
from the Hangfire dashboard (mapped with the admin area in Stage 7).
