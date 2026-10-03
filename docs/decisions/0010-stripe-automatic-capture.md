# 0010 — Stripe: automatic capture, webhook as source of truth

- Status: Accepted
- Date: 2026-10-03
- Deciders: GaiaSkyline maintainers

## Context

Bookings are often made weeks or months before check-in and must hold the dates until then. Stripe
offers manual capture (authorize now, capture later), which is the usual way to "hold" a card.

## Decision

**Use automatic capture for every payment method.** We do not authorize-and-capture-later.

- A card authorization **expires after ~7 days**, so manual capture cannot hold a booking made weeks
  ahead — the authorization would lapse before check-in.
- **Multibanco doesn't support manual capture** at all (it's a pay-by-reference voucher).
- Cancellations are therefore handled by **refunds** according to the cancellation policy (ADR 0009),
  not by releasing an uncaptured authorization.

**The webhook is the source of truth for confirmation**, not the browser redirect:
- PaymentIntent is created with `automatic_payment_methods` so dashboard-enabled methods appear in the
  Payment Element. Multibanco is excluded (`excluded_payment_method_types`) when check-in is within the
  configured lead time (default 10 days), since the voucher needs time to be paid.
- `payment_intent.succeeded` → Confirmed; `processing` → stay AwaitingPayment (persist the Multibanco
  voucher); `payment_failed` → card decline stays AwaitingPayment (retry until the hold expires),
  Multibanco expiry cancels and releases the dates; `charge.refunded` → Refunded/PartiallyRefunded.
- Every event is recorded in `StripeEventLog` first (unique event id → idempotent); a handler failure
  records the error and returns 500 so Stripe retries.

**Unpaid holds** are released by a Hangfire job every 5 minutes in addition to webhooks, so dates are
freed even if a webhook is missed: card/wallet holds past the 30-minute window, Multibanco past its
voucher expiry (taken from Stripe, never hardcoded).

## Consequences

- **+** A booking made at any lead time is paid immediately and the dates are genuinely secured.
- **+** Multibanco works (it can't be held by authorization anyway).
- **+** One consistent cancellation path (refund), driven by policy.
- **−** Money is captured up front, so every cancellation is a refund — acceptable and expected for a
  direct-booking stay, and matches the "Free Cancellation" window.
- **−** Confirmation depends on receiving the webhook; mitigated by the 500-retry contract and the
  expiry safety-net job.
